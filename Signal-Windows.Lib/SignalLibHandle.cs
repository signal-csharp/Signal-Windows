using libsignal;
using libsignalservice;
using libsignalservice.messages;
using libsignalservice.util;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Signal_Windows.Lib.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Windows.UI.Core;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.Web;

namespace Signal_Windows.Lib
{
    public interface ISignalFrontend
    {
        void AddOrUpdateConversation(SignalConversation conversation, SignalMessage updateMessage);
        void HandleMessage(SignalMessage message, SignalConversation conversation);
        void HandleIdentitykeyChange(LinkedList<SignalMessage> messages);
        void HandleMessageUpdate(SignalMessage updatedMessage);
        void ReplaceConversationList(List<SignalConversation> conversations);
        void HandleAuthFailure();
        void HandleAttachmentStatusChanged(SignalAttachment sa);
    }

    public interface ISignalLibHandle
    {
        //Frontend API
        SignalStore Store { get; set; }
        Task SendMessage(SignalMessage message, SignalConversation conversation);
        void ResendMessage(SignalMessage message);
        List<SignalMessageContainer> GetMessages(SignalConversation thread, int startIndex, int count);
        void SaveAndDispatchSignalConversation(SignalConversation updatedConversation, SignalMessage updateMessage);
        void PurgeAccountData();
        Task Acquire(CoreDispatcher d, ISignalFrontend w);
        Task Reacquire();
        void Release();
        void AddFrontend(CoreDispatcher d, ISignalFrontend w);
        void RemoveFrontend(CoreDispatcher d);

        // Background API
        event EventHandler<SignalMessageEventArgs> SignalMessageEvent;
        void BackgroundAcquire();
        void BackgroundRelease();

        // Attachment API
        void StartAttachmentDownload(SignalAttachment sa);
        //void AbortAttachmentDownload(SignalAttachment sa); TODO
    }

    public static class SignalHelper
    {
        public static ISignalLibHandle CreateSignalLibHandle(bool headless)
        {
            return new SignalLibHandle(headless);
        }
    }

    internal class SignalLibHandle : ISignalLibHandle
    {
        internal static SignalLibHandle Instance;
        public SignalStore Store { get; set; }
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
        private EventWaitHandle GlobalResetEvent;
        private Dictionary<long, DownloadOperation> Downloads = new Dictionary<long, DownloadOperation>();
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
            GlobalResetEvent = LibUtils.OpenResetEventSet();
            LibUtils.Lock();
            GlobalResetEvent.Reset();
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
                RecoverDownloads().Wait();
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
            RecoverDownloads().Wait();
            Running = true;
        }

        public async Task Reacquire()
        {
            Logger.LogTrace("Reacquire() locking");
            CancelSource = new CancellationTokenSource();
            SemaphoreSlim.Wait(CancelSource.Token);
            GlobalResetEvent = LibUtils.OpenResetEventSet();
            LibUtils.Lock();
            GlobalResetEvent.Reset();
            LibsignalDBContext.ClearSessionCache();
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
                Downloads.Clear();
                RecoverDownloads().Wait();
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
            Logger.LogTrace("Release() releasing global)");
            LibUtils.Unlock();
            Logger.LogTrace("Release() releasing local)");
            SemaphoreSlim.Release();
            Logger.LogTrace("Release() released");
        }

        public void BackgroundRelease()
        {
            Running = false;
            CancelSource.Cancel();
            IncomingMessagesTask?.Wait();
            OutgoingMessagesTask?.Wait();
            Instance = null;
        }

        public async Task SendMessage(SignalMessage message, SignalConversation conversation)
        {
            await Task.Run(() =>
            {
                Logger.LogTrace("SendMessage() locking");
                SemaphoreSlim.Wait(CancelSource.Token);
                Logger.LogTrace("SendMessage() locked");
                try
                {
                    Logger.LogDebug("SendMessage saving message " + message.ComposedTimestamp);
                    SaveAndDispatchSignalMessage(message, conversation);
                    OutgoingQueue.Add(message);
                }
                finally
                {
                    SemaphoreSlim.Release();
                    Logger.LogTrace("SendMessage() released");
                }
            });
        }

        public void ResendMessage(SignalMessage message)
        {
            OutgoingQueue.Add(message);
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

        public string RetrieveAttachmentUrl(SignalServiceAttachmentPointer pointer)
        {
            return MessageReceiver.RetrieveAttachmentDownloadUrl(pointer);
        }

        public void DecryptAttachment(SignalServiceAttachmentPointer pointer, Stream tempStream, Stream downloadStream)
        {
            MessageReceiver.DecryptAttachment(pointer, tempStream, downloadStream);
        }
        #endregion

        #region attachment api
        public void StartAttachmentDownload(SignalAttachment sa)
        {
            //TODO lock, check if already downloading, start a new download if not exists
            Task.Run(() =>
            {
                try
                {
                    Logger.LogTrace("StartAttachmentDownload() locking");
                    SemaphoreSlim.Wait(CancelSource.Token);
                    Logger.LogTrace("StartAttachmentDownload() locked");
                    TryScheduleAttachmentDownload(sa);
                }
                catch(Exception e)
                {
                    Logger.LogError("StartAttachmentDownload failed: {0}\n{1}", e.Message, e.StackTrace);
                }
                finally
                {
                    SemaphoreSlim.Release();
                    Logger.LogTrace("StartAttachmentDownload() released");
                }
            });
        }
        #endregion

        #region internal api
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
            //StartAttachmentDownloads(message);
            DispatchHandleMessage(message, conversation);
        }

        internal void DispatchHandleIdentityKeyChange(LinkedList<SignalMessage> messages)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                operations.Add(dispatcher.RunTaskAsync(() =>
                {
                    Frames[dispatcher].HandleIdentitykeyChange(messages);
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
                operations.Add(dispatcher.RunTaskAsync(() =>
                {
                    Frames[dispatcher].HandleMessage(message, conversation);
                }));
            }
            SignalMessageEvent?.Invoke(this, new SignalMessageEventArgs(message, Events.SignalMessageType.NormalMessage));
            Task.WaitAll(operations.ToArray());
        }

        internal void DispatchPipeEmptyMessage()
        {
            SignalMessageEvent?.Invoke(this, new SignalMessageEventArgs(null, Events.SignalMessageType.PipeEmptyMessage));
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

        private void TryScheduleAttachmentDownload(SignalAttachment attachment)
        {
            if (Downloads.Count < 100)
            {
                if (attachment.Status != SignalAttachmentStatus.Finished && !Downloads.ContainsKey(attachment.Id))
                {
                    SignalServiceAttachmentPointer attachmentPointer = attachment.ToAttachmentPointer();
                    IStorageFolder localFolder = ApplicationData.Current.LocalFolder;
                    IStorageFile tmpDownload = Task.Run(async () =>
                    {
                        return await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(@"Attachments\" + attachment.Id + ".cipher");
                    }).Result;
                    BackgroundDownloader downloader = new BackgroundDownloader();
                    downloader.SetRequestHeader("Content-Type", "application/octet-stream");
                    downloader.SuccessToastNotification = LibUtils.CreateToastNotification($"{attachment.SentFileName} has finished downloading.");
                    downloader.FailureToastNotification = LibUtils.CreateToastNotification($"{attachment.SentFileName} has failed to download.");
                    // this is the recommended way to call CreateDownload
                    // see https://docs.microsoft.com/en-us/uwp/api/windows.networking.backgroundtransfer.backgrounddownloader#Methods
                    DownloadOperation download = downloader.CreateDownload(new Uri(RetrieveAttachmentUrl(attachmentPointer)), tmpDownload);
                    attachment.Guid = "" + download.Guid;
                    SignalDBContext.UpdateAttachmentGuid(attachment);
                    Downloads.Add(attachment.Id, download);
                    Task.Run(async () =>
                    {
                        Logger.LogInformation("Waiting for download {0}({1})", attachment.SentFileName, attachment.Id);
                        var t = await download.StartAsync();
                        await HandleSuccessfullDownload(attachment, tmpDownload, download);
                    });
                }
            }
        }

        private async Task HandleSuccessfullDownload(SignalAttachment attachment, IStorageFile tmpDownload, DownloadOperation download)
        {
            try
            {
                SemaphoreSlim.Wait(CancelSource.Token);
                string fileExtension = LibUtils.GetAttachmentExtension(attachment);
                StorageFile plaintextFile = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync($@"Attachments\{attachment.Id}.{fileExtension}", CreationCollisionOption.ReplaceExisting);
                using (var tmpFileStream = (await tmpDownload.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
                using (var plaintextFileStream = (await plaintextFile.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
                {
                    Logger.LogInformation("Decrypting to {0}\\{1}", plaintextFile.Path, plaintextFile.Name);
                    DecryptAttachment(attachment.ToAttachmentPointer(), tmpFileStream, plaintextFileStream);
                }
                Logger.LogInformation("Deleting tmpFile {0}", tmpDownload.Name);
                await tmpDownload.DeleteAsync();
                attachment.Status = SignalAttachmentStatus.Finished;
                SignalDBContext.UpdateAttachmentStatus(attachment);
                DispatchAttachmentStatusChanged(download, attachment);
            }
            catch(Exception e)
            {
                Logger.LogError("HandleSuccessfullDownload failed: {0}\n{1}", e.Message, e.StackTrace);
            }
            finally
            {
                if (Downloads.ContainsKey(attachment.Id))
                {
                    Downloads.Remove(attachment.Id);
                }
                SemaphoreSlim.Release();
            }
        }

        private async Task RecoverDownloads()
        {
            var downloads = await BackgroundDownloader.GetCurrentDownloadsAsync();
            foreach (DownloadOperation download in downloads)
            {
                try
                {
                    SignalAttachment attachment = SignalDBContext.GetAttachmentByGuidNameLocked(download.Guid.ToString());
                    if (attachment != null)
                    {
                        Downloads.Add(attachment.Id, download);
                        var t = Task.Run(async () =>
                        {
                            Logger.LogInformation("Attaching to download {0} ({1})", attachment.Id, download.Guid);
                            await download.AttachAsync();
                            await HandleSuccessfullDownload(attachment, download.ResultFile, download);
                        });
                    }
                    else
                    {
                        Logger.LogInformation("Aborting unrecognized download {0}", download.Guid);
                        download.AttachAsync().Cancel();
                    }
                }
                catch(Exception e)
                {
                    Logger.LogError("TriageDownloads encountered an error: {0}\n{1}", e.Message, e.StackTrace);
                }
            }
        }

        private void DispatchAttachmentStatusChanged(DownloadOperation op, SignalAttachment attachment)
        {
            try
            {
                List<Task> operations = new List<Task>();
                foreach (var dispatcher in Frames.Keys)
                {
                    operations.Add(dispatcher.RunTaskAsync(() =>
                    {
                        Frames[dispatcher].HandleAttachmentStatusChanged(attachment);
                    }));
                }
                Task.WaitAll(operations.ToArray());
            }
            catch(Exception e)
            {
                Logger.LogError("DispatchAttachmentStatusChanged encountered an error: {0}\n{1}", e.Message, e.StackTrace);
            }
        }
        #endregion
    }
}
