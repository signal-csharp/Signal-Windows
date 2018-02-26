using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using libsignalservice;
using Windows.UI.Core;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Microsoft.Extensions.Logging;
using System.Threading;
using libsignalservice.util;
using System.Collections.Concurrent;
using libsignal;
using System.Diagnostics;
using Signal_Windows.Lib.Events;
using libsignalservice.messages;
using System.IO;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.Web;

namespace Signal_Windows.Lib
{
    public interface ISignalFrontend
    {
        void AddOrUpdateConversation(SignalConversation conversation, SignalMessage updateMessage);
        Task HandleMessage(SignalMessage message, SignalConversation conversation);
        Task HandleIdentitykeyChange(LinkedList<SignalMessage> messages);
        void HandleMessageUpdate(SignalMessage updatedMessage);
        void ReplaceConversationList(List<SignalConversation> conversations);
        void HandleAuthFailure();
    }

    public class SignalLibHandle
    {
        public static SignalLibHandle Instance;
        public SignalStore Store;
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<SignalLibHandle>();
        public SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);
        private bool Headless;
        private bool Running = false;
        private CancellationTokenSource CancelSource = new CancellationTokenSource();
        private Dictionary<CoreDispatcher, ISignalFrontend> Frames = new Dictionary<CoreDispatcher, ISignalFrontend>();
        private Task IncomingMessagesTask;
        private Task OutgoingMessagesTask;
        private SignalServiceMessagePipe Pipe;
        private SignalServiceMessageSender MessageSender;
        private SignalServiceMessageReceiver MessageReceiver;
        public BlockingCollection<SignalMessage> OutgoingQueue = new BlockingCollection<SignalMessage>(new ConcurrentQueue<SignalMessage>());

        public event EventHandler<SignalMessageEventArgs> SignalMessageEvent;

        #region frontend api
        public SignalLibHandle(bool headless)
        {
            Headless = headless;
            Instance = this;
        }

        public void AddFrontend(CoreDispatcher d, ISignalFrontend w)
        {
            Logger.LogTrace("AddFrontend() locking");
            SemaphoreSlim.Wait(CancelSource.Token);
            Logger.LogTrace("AddFrontend() locked");
            if (Running)
            {
                Logger.LogInformation("Registering frontend of dispatcher {0}", w.GetHashCode());
                Frames.Add(d, w);
                w.ReplaceConversationList(GetConversations());
            }
            else
            {
                Logger.LogInformation("Ignoring AddFrontend call, release in progress");
            }
            SemaphoreSlim.Release();
            Logger.LogTrace("AddFrontend() released");
        }

        public void RemoveFrontend(CoreDispatcher d)
        {
            Logger.LogTrace("RemoveFrontend() locking");
            SemaphoreSlim.Wait(CancelSource.Token);
            Logger.LogTrace("RemoveFrontend() locked");
            Logger.LogInformation("Unregistering frontend of dispatcher {0}", d.GetHashCode());
            Frames.Remove(d);
            SemaphoreSlim.Release();
            Logger.LogTrace("RemoveFrontend() released");
        }

        public void PurgeAccountData()
        {
            Logger.LogTrace("PurgeAccountData() locking");
            SemaphoreSlim.Wait(CancelSource.Token);
            Logger.LogTrace("PurgeAccountData() locked");
            LibsignalDBContext.PurgeAccountData();
            SemaphoreSlim.Release();
            Logger.LogTrace("PurgeAccountData() released");
        }

        public async Task Acquire(CoreDispatcher d, ISignalFrontend w) //TODO wrap trycatch dispatch auth failure
        {
            Logger.LogTrace("Acquire() locking");
            CancelSource = new CancellationTokenSource();
            SemaphoreSlim.Wait(CancelSource.Token);
            LibUtils.Lock();
            var getConversationsTask = Task.Run(() =>
            {
                return GetConversations(); // we want to display the conversations asap!
            });
            Logger.LogDebug("Acquire() locked (global and local)");
            Instance = this;
            Frames.Add(d, w);
            w.ReplaceConversationList(await getConversationsTask);
            var failTask = Task.Run(() =>
            {
                SignalDBContext.FailAllPendingMessages(); // TODO GetMessages needs to be protected by semaphoreslim as we fail defered
            });
            Store = await Task.Run(() =>
            {
                return LibsignalDBContext.GetSignalStore();
            });
            if (Store == null)
            {
                SemaphoreSlim.Release();
                throw new Exception("Signal Store has not been setup yet.");
            }
            await Task.Run(() =>
            {
                InitNetwork();
            });
            await failTask; // has to complete before messages are loaded
            Running = true;
            Logger.LogTrace("Acquire() releasing");
            SemaphoreSlim.Release();
        }

        public void BackgroundAcquire()
        {
            CancelSource = new CancellationTokenSource();
            Instance = this;
            SignalDBContext.FailAllPendingMessages();
            Store = LibsignalDBContext.GetSignalStore();
            InitNetwork();
            Running = true;
        }

        public async Task Reacquire()
        {
            Logger.LogTrace("Reacquire() locking");
            CancelSource = new CancellationTokenSource();
            SemaphoreSlim.Wait(CancelSource.Token);
            LibUtils.Lock();
            Instance = this;
            await Task.Run(() =>
            {
                List<Task> tasks = new List<Task>();
                foreach (var f in Frames)
                {
                    var conversations = GetConversations();
                    tasks.Add(f.Key.RunTaskAsync(() =>
                    {
                        f.Value.ReplaceConversationList(conversations);
                    }));
                }
                Task.WaitAll(tasks.ToArray());
                InitNetwork();
            });
            Running = true;
            Logger.LogTrace("Reacquire() releasing");
            SemaphoreSlim.Release();
        }

        public void Release()
        {
            //TODO invalidate view information
            Logger.LogTrace("Release() locking");
            SemaphoreSlim.Wait(CancelSource.Token);
            Running = false;
            CancelSource.Cancel();
            IncomingMessagesTask?.Wait();
            OutgoingMessagesTask?.Wait();
            Instance = null;
            Logger.LogTrace("Release() releasing (global and local)");
            LibUtils.Unlock();
            SemaphoreSlim.Release();
        }

        public void BackgroundRelease()
        {
            Running = false;
            CancelSource.Cancel();
            IncomingMessagesTask?.Wait();
            OutgoingMessagesTask?.Wait();
            Instance = null;
        }

        public async Task AddContact(string username)
        {
            // this is very brute force atm
            SignalDBContext.GetOrCreateContactLocked(username, Util.CurrentTimeMillis());
            await RefreshConversationList();
        }

        public async Task RefreshConversationList()
        {
            await Task.Run(() =>
            {
                List<Task> tasks = new List<Task>();
                foreach (var dispatcher in Frames.Keys)
                {
                    var conversations = GetConversations();
                    tasks.Add(dispatcher.RunTaskAsync(() =>
                    {
                        Frames[dispatcher].ReplaceConversationList(conversations);
                    }));
                }
                Task.WaitAll(tasks.ToArray());
            });
        }

        public async Task SendMessage(SignalMessage message, SignalConversation conversation)
        {
            await Task.Run(async () =>
            {
                Logger.LogTrace("SendMessage() locking");
                await SemaphoreSlim.WaitAsync(CancelSource.Token);
                Logger.LogDebug("SendMessage saving message " + message.ComposedTimestamp);
                SaveAndDispatchSignalMessage(message, conversation);
                OutgoingQueue.Add(message);
                SemaphoreSlim.Release();
                Logger.LogTrace("SendMessage() released");
            });
        }

        public List<SignalMessageContainer> GetMessages(SignalConversation thread, int startIndex, int count)
        {
            return SignalDBContext.GetMessagesLocked(thread, startIndex, count);
        }

        public void SaveAndDispatchSignalConversation(SignalConversation updatedConversation, SignalMessage updateMessage)
        {
            Logger.LogTrace("SaveAndDispatchSignalConversation() locking");
            SemaphoreSlim.Wait(CancelSource.Token);
            SignalDBContext.InsertOrUpdateConversationLocked(updatedConversation);
            DispatchAddOrUpdateConversation(updatedConversation, updateMessage);
            SemaphoreSlim.Release();
            Logger.LogTrace("SaveAndDispatchSignalConversation() released");
        }

        public void RetrieveAttachment(SignalServiceAttachmentPointer pointer, Stream downloadStream, Stream tempStream)
        {
            MessageReceiver.retrieveAttachment(pointer, downloadStream, tempStream, 0);
        }

        public string RetrieveAttachmentDownloadUrl(SignalServiceAttachmentPointer pointer)
        {
            return MessageReceiver.RetrieveAttachmentDownloadUrl(pointer);
        }

        /// <summary>
        /// Gets a URL that can be used to upload an attachment
        /// </summary>
        /// <returns>The attachment ID and the URL</returns>
        public (ulong id, string location) RetrieveAttachmentUploadUrl()
        {
            return MessageSender.RetrieveAttachmentUploadUrl();
        }

        /// <summary>
        /// Encrypts an attachment to be uploaded
        /// </summary>
        /// <param name="data">The data stream of the attachment</param>
        /// <param name="key">64 random bytes</param>
        /// <returns>The digest and the encrypted data</returns>
        public (byte[] digest, Stream encryptedData) EncryptAttachment(Stream data, byte[] key)
        {
            return MessageSender.EncryptAttachment(data, key);
        }

        public void DecryptAttachment(SignalServiceAttachmentPointer pointer, Stream tempStream, Stream downloadStream)
        {
            MessageReceiver.DecryptAttachment(pointer, tempStream, downloadStream);
        }

        public async Task HandleDownload(DownloadOperation download, bool start, SignalAttachment attachment)
        {
            try
            {
                if (start)
                {
                    await download.StartAsync();
                }
                else
                {
                    await download.AttachAsync();
                }
            }
            catch (Exception ex)
            {
                WebErrorStatus errorStatus = BackgroundTransferError.GetStatus(ex.HResult);
                if (errorStatus == WebErrorStatus.NotFound)
                {
                    attachment.Status = SignalAttachmentStatus.Failed_Permanently;
                }
                else
                {
                    attachment.Status = SignalAttachmentStatus.Failed;
                }
                await download.ResultFile.DeleteAsync();
                SignalDBContext.UpdateAttachmentLocked(attachment);
                return;
            }
            IStorageFile tempFile = download.ResultFile;
            string fileName = attachment.SentFileName;
            if (string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(attachment.FileName))
            {
                fileName = attachment.FileName;
            }
            StorageFile downloadedFile = await DownloadsFolder.CreateFileAsync(LibUtils.EnsureSafeFilename(fileName), CreationCollisionOption.GenerateUniqueName);
            using (var tempFileStream = (await tempFile.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
            using (var downloadedFileStream = (await downloadedFile.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
            {
                DecryptAttachment(attachment.ToAttachmentPointer(), tempFileStream, downloadedFileStream);
            }
            attachment.Status = SignalAttachmentStatus.Finished;
            SignalDBContext.UpdateAttachmentLocked(attachment);
            await tempFile.DeleteAsync();
        }

        public async Task HandleUpload(UploadOperation upload, bool start, SignalMessage message)
        {
            try
            {
                if (start)
                {
                    await upload.StartAsync();
                }
                else
                {
                    await upload.AttachAsync();
                }
            }
            catch (Exception ex)
            {
                WebErrorStatus errorStatus = BackgroundTransferError.GetStatus(ex.HResult);
                await upload.SourceFile.DeleteAsync();
                return;
            }
            IStorageFile tempFile = upload.SourceFile;
            await tempFile.DeleteAsync();
            foreach (var attachment in message.Attachments)
            {
                attachment.Status = SignalAttachmentStatus.Finished;
                SignalDBContext.UpdateAttachmentLocked(attachment);
            }

            SignalConversation conversation = SignalDBContext.GetConversationByThreadId(message.ThreadId);
            if (conversation != null)
            {
                await Task.Run(async () =>
                {
                    await SemaphoreSlim.WaitAsync(CancelSource.Token);
                    conversation.LastMessage = message;
                    conversation.LastActiveTimestamp = message.ComposedTimestamp;
                    DispatchHandleMessage(message, conversation);
                    OutgoingQueue.Add(message);
                    SemaphoreSlim.Release();
                });
            }
        }
        #endregion

        #region backend api
        internal void SaveAndDispatchSignalMessage(SignalMessage message, SignalConversation conversation)
        {
            conversation.MessagesCount += 1;
            if (message.Direction == SignalMessageDirection.Incoming)
            {
                conversation.UnreadCount += 1;
            }
            else
            {
                conversation.UnreadCount = 0;
                conversation.LastSeenMessageIndex = conversation.MessagesCount;
            }
            SignalDBContext.SaveMessageLocked(message);
            conversation.LastMessage = message;
            conversation.LastActiveTimestamp = message.ComposedTimestamp;
            DispatchHandleMessage(message, conversation);
        }

        internal void DispatchHandleIdentityKeyChange(LinkedList<SignalMessage> messages)
        {
            List<Task> operations = new List<Task>(); ;
            foreach (var dispatcher in Frames.Keys)
            {
                operations.Add(dispatcher.RunTaskAsync(async () =>
                {
                    await Frames[dispatcher].HandleIdentitykeyChange(messages);
                }));
            }
            Task.WaitAll(operations.ToArray());
        }

        internal void DispatchAddOrUpdateConversation(SignalConversation conversation, SignalMessage updateMessage)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                operations.Add(dispatcher.RunTaskAsync(() =>
                {
                    Frames[dispatcher].AddOrUpdateConversation(conversation, updateMessage);
                }));
            }
            Task.WaitAll(operations.ToArray());
        }

        internal void DispatchHandleMessage(SignalMessage message, SignalConversation conversation)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                operations.Add(dispatcher.RunTaskAsync(async () =>
                {
                    await Frames[dispatcher].HandleMessage(message, conversation);
                }));
            }
            SignalMessageEvent?.Invoke(this, new SignalMessageEventArgs(message));
            Task.WaitAll(operations.ToArray());
        }

        internal void HandleMessageSentLocked(SignalMessage msg)
        {
            Logger.LogTrace("HandleMessageSentLocked() locking");
            SemaphoreSlim.Wait(CancelSource.Token);
            Logger.LogTrace("HandleMessageSentLocked() locked");
            var updated = SignalDBContext.UpdateMessageStatus(msg);
            DispatchMessageUpdate(updated);
            SemaphoreSlim.Release();
            Logger.LogTrace("HandleMessageSentLocked() released");
        }

        internal void DispatchMessageUpdate(SignalMessage msg)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                operations.Add(dispatcher.RunTaskAsync(() =>
                {
                    Frames[dispatcher].HandleMessageUpdate(msg);
                }));
            }
            Task.WaitAll(operations.ToArray());
        }

        internal void HandleOutgoingKeyChangeLocked(string user, string identity)
        {
            Logger.LogTrace("HandleOutgoingKeyChange() locking");
            SemaphoreSlim.Wait(CancelSource.Token);
            Logger.LogTrace("HandleOutgoingKeyChange() locked");
            var messages = LibsignalDBContext.InsertIdentityChangedMessagesLocked(user);
            LibsignalDBContext.SaveIdentityLocked(new SignalProtocolAddress(user, 1), identity);
            DispatchHandleIdentityKeyChange(messages);
            SemaphoreSlim.Release();
            Logger.LogTrace("HandleOutgoingKeyChange() released");
        }
        #endregion

        #region private
        private List<SignalConversation> GetConversations()
        {
            List<SignalConversation> conversations = new List<SignalConversation>();
            List<SignalContact> contacts = SignalDBContext.GetAllContactsLocked();
            List<SignalGroup> groups = SignalDBContext.GetAllGroupsLocked();
            int amountContacts = contacts.Count;
            int amountGroups = groups.Count;
            int contactsIdx = 0;
            int groupsIdx = 0;
            while (contactsIdx < amountContacts || groupsIdx < amountGroups)
            {
                if (contactsIdx < amountContacts)
                {
                    SignalConversation contact = contacts[contactsIdx];
                    if (groupsIdx < amountGroups)
                    {
                        SignalConversation group = groups[groupsIdx];
                        if (contact.LastActiveTimestamp > group.LastActiveTimestamp)
                        {
                            contactsIdx++;
                            conversations.Add(contact);
                        }
                        else
                        {
                            groupsIdx++;
                            conversations.Add(group);
                        }
                    }
                    else
                    {
                        contactsIdx++;
                        conversations.Add(contact);
                    }
                }
                else if (groupsIdx < amountGroups)
                {
                    SignalConversation group = groups[groupsIdx];
                    groupsIdx++;
                    conversations.Add(group);
                }
            }
            return conversations;
        }

        private void InitNetwork()
        {
            MessageReceiver = new SignalServiceMessageReceiver(CancelSource.Token, LibUtils.ServiceUrls, new StaticCredentialsProvider(Store.Username, Store.Password, Store.SignalingKey, (int)Store.DeviceId), LibUtils.USER_AGENT);
            Pipe = MessageReceiver.createMessagePipe();
            MessageSender = new SignalServiceMessageSender(CancelSource.Token, LibUtils.ServiceUrls, Store.Username, Store.Password, (int)Store.DeviceId, new Store(), Pipe, null, LibUtils.USER_AGENT);
            IncomingMessagesTask = Task.Factory.StartNew(() => new IncomingMessages(CancelSource.Token, Pipe, this).HandleIncomingMessages(), TaskCreationOptions.LongRunning);
            OutgoingMessagesTask = Task.Factory.StartNew(() => new OutgoingMessages(CancelSource.Token, MessageSender, this).HandleOutgoingMessages(), TaskCreationOptions.LongRunning);
        }
        #endregion
    }
}
