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
using libsignalservice.push;
using Strilanc.Value;
using libsignalservice.messages.multidevice;
using libsignalservice.crypto;
using libsignalservice.configuration;
using libsignal_service_dotnet.messages.calls;

namespace Signal_Windows.Lib
{
    public class SignalLibConstants
    {
        public static SignalServiceUrl[] ServiceUrls = new SignalServiceUrl[] { new SignalServiceUrl("https://textsecure-service.whispersystems.org") };
        public static SignalServiceConfiguration ServiceConfiguration = new SignalServiceConfiguration(ServiceUrls, null);
        public static string USER_AGENT = "Signal-Windows";
    }

    public class AppendResult
    {
        public long Index { get;  }
        public AppendResult(long index)
        {
            Index = index;
        }
    }

    public interface ISignalFrontend
    {
        void AddOrUpdateConversation(SignalConversation conversation, SignalMessage updateMessage);
        AppendResult HandleMessage(SignalMessage message, SignalConversation conversation);
        void HandleUnreadMessage(SignalMessage message);
        void HandleMessageRead(long unreadMarkerIndex, SignalConversation conversation);
        void HandleIdentitykeyChange(LinkedList<SignalMessage> messages);
        void HandleMessageUpdate(SignalMessage updatedMessage);
        void ReplaceConversationList(List<SignalConversation> conversations);
        void HandleAuthFailure();
        void HandleAttachmentStatusChanged(SignalAttachment sa);
        Task HandleCallOfferMessage(SignalServiceEnvelope envelope, OfferMessage offerMessage);
        Task HandleCallIceUpdatesMessage(SignalServiceEnvelope envelope, List<IceUpdateMessage> iceUpdateMessages);
    }

    public interface ISignalLibHandle
    {
        //Frontend API
        SignalStore Store { get; set; }

        void RequestSync();
        Task SendMessage(SignalMessage message, SignalConversation conversation);
        Task SetMessageRead(long index, SignalMessage message, SignalConversation conversation);
        void ResendMessage(SignalMessage message);
        List<SignalMessageContainer> GetMessages(SignalConversation thread, int startIndex, int count);
        void SaveAndDispatchSignalConversation(SignalConversation updatedConversation, SignalMessage updateMessage);
        void PurgeAccountData();
        Task<bool> Acquire(CoreDispatcher d, ISignalFrontend w);
        Task Reacquire();
        void Release();
        bool AddFrontend(CoreDispatcher d, ISignalFrontend w);
        void RemoveFrontend(CoreDispatcher d);
        SignalServiceAccountManager CreateAccountManager();

        // Background API
        event EventHandler<SignalMessageEventArgs> SignalMessageEvent;
        void BackgroundAcquire();
        void BackgroundRelease();

        // Attachment API
        void StartAttachmentDownload(SignalAttachment sa);
        //void AbortAttachmentDownload(SignalAttachment sa); TODO

        // Calls API
        void SendCallResponse(string recipient, ulong id, string description);
        void SendCallMessage(string v, SignalServiceCallMessage msg);
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
        private readonly bool Headless;
        private bool Running = false;
        private bool LikelyHasValidStore = false;
        private CancellationTokenSource CancelSource = new CancellationTokenSource();
        private Dictionary<CoreDispatcher, ISignalFrontend> Frames = new Dictionary<CoreDispatcher, ISignalFrontend>();
        private CoreDispatcher MainWindowDispatcher;
        private ISignalFrontend MainWindow;
        private Task IncomingMessagesTask;
        private Task OutgoingMessagesTask;
        internal OutgoingMessages OutgoingMessages;
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

        public bool AddFrontend(CoreDispatcher d, ISignalFrontend w)
        {
            Logger.LogTrace("AddFrontend() locking");
            SemaphoreSlim.Wait(CancelSource.Token);
            try
            {
                Logger.LogTrace("AddFrontend() locked");
                if (Running && LikelyHasValidStore)
                {
                    Logger.LogInformation("Registering frontend of dispatcher {0}", w.GetHashCode());
                    Frames.Add(d, w);
                    w.ReplaceConversationList(GetConversations());
                    return true;
                }
                else
                {
                    Logger.LogInformation("Ignoring AddFrontend call");
                    return false;
                }
            }
            finally
            {
                SemaphoreSlim.Release();
                Logger.LogTrace("AddFrontend() released");
            }
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

        public async Task<bool> Acquire(CoreDispatcher d, ISignalFrontend w) //TODO wrap trycatch dispatch auth failure
        {
            Logger.LogTrace("Acquire() locking");
            CancelSource = new CancellationTokenSource();
            SemaphoreSlim.Wait(CancelSource.Token);
            try
            {
                GlobalResetEvent = LibUtils.OpenResetEventSet();
                LibUtils.Lock();
                GlobalResetEvent.Reset();
                MainWindowDispatcher = d;
                MainWindow = w;
                Logger.LogDebug("Acquire() locked (global and local)");
                var getConversationsTask = Task.Run(() =>
                {
                    return GetConversations(); // we want to display the conversations asap!
                });
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
                    return false;
                }
                else
                {
                    LikelyHasValidStore = true;
                }
                var initNetwork = Task.Run(() =>
                {
                    InitNetwork();
                });
                var recoverDownloadsTask = Task.Run(() =>
                {
                    RecoverDownloads().Wait();
                });
                await failTask; // has to complete before messages are loaded
                await recoverDownloadsTask;
                Running = true;
                return true;
            }
            finally
            {
                SemaphoreSlim.Release();
                Logger.LogTrace("Acquire() released");
            }
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
            try
            {
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
                    RecoverDownloads().Wait();
                    Store = LibsignalDBContext.GetSignalStore();
                    if (Store != null)
                    {
                        LikelyHasValidStore = true;
                    }
                });
                if (LikelyHasValidStore)
                {
                    var initNetworkTask = Task.Run(() =>
                    {
                        InitNetwork();
                    });
                }
                Running = true;
            }
            finally
            {
                SemaphoreSlim.Release();
                Logger.LogTrace("Reacquire() released");
            }
        }

        public void Release()
        {
            //TODO invalidate view information
            Logger.LogTrace("Release() locking");
            if (Running)
            {
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
            else
            {
                Logger.LogTrace("SignalLibHandle was already closed");
            }
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

        internal void HandleIncomingCallOffer(SignalServiceEnvelope envelope, OfferMessage offerMessage)
        {
            Task.Run(async () =>
            {
                await DispatchCallOfferMessage(envelope, offerMessage);
            });
            
        }

        internal void HandleIncomingIceUpdate(SignalServiceEnvelope envelope, List<IceUpdateMessage> iceUpdateMessages)
        {
            Task.Run(async () =>
            {
                await DispatchCallIncomingIceUpdate(envelope, iceUpdateMessages);
            });
        }

        public void RequestSync()
        {
            Task.Run(() =>
            {
                Logger.LogTrace("RequestSync()");
                var contactsRequest = SignalServiceSyncMessage.ForRequest(new RequestMessage(new SyncMessage.Types.Request()
                {
                    Type = SyncMessage.Types.Request.Types.Type.Contacts
                }));
                OutgoingMessages.SendMessage(contactsRequest);
                var groupsRequest = SignalServiceSyncMessage.ForRequest(new RequestMessage(new SyncMessage.Types.Request()
                {
                    Type = SyncMessage.Types.Request.Types.Type.Groups
                }));
                OutgoingMessages.SendMessage(groupsRequest);
            });
        }

        /// <summary>
        /// Marks and dispatches a message as read. Must not be called on a task which holds the handle lock.
        /// </summary>
        /// <param name="message"></param>
        public async Task SetMessageRead(long index, SignalMessage message, SignalConversation conversation)
        {
            Logger.LogTrace("SetMessageRead() locking");
            await SemaphoreSlim.WaitAsync(CancelSource.Token);
            try
            {
                Logger.LogTrace("SetMessageRead() locked");
                conversation = SignalDBContext.UpdateMessageRead(index, conversation);
                OutgoingMessages.SendMessage(SignalServiceSyncMessage.ForRead(new List<ReadMessage>() {
                        new ReadMessage(message.Author.ThreadId, message.ComposedTimestamp)
                }));
                await DispatchMessageRead(index + 1, conversation);
            }
            finally
            {
                SemaphoreSlim.Release();
                Logger.LogTrace("SetMessageRead() released");
            }
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

        public SignalServiceAccountManager CreateAccountManager()
        {
            return new SignalServiceAccountManager(SignalLibConstants.ServiceConfiguration, Store.Username, Store.Password, (int)Store.DeviceId, SignalLibConstants.USER_AGENT);
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

        #region calls api
        public void SendCallMessage(string recipient, SignalServiceCallMessage message)
        {
            Logger.LogDebug("SendCallMessage()");
            OutgoingMessages.SendMessage(recipient, message);
        }

        public void SendCallResponse(string recipient, ulong id, string description)
        {
            Logger.LogDebug("SendCallResponse()");
            var answer = new SignalServiceCallMessage()
            {
                AnswerMessage = new AnswerMessage()
                {
                    Description = description,
                    Id = id
                }
            };
            OutgoingMessages.SendMessage(recipient, answer);
        }

        #endregion
        #region internal api
        internal void DispatchHandleAuthFailure()
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                operations.Add(dispatcher.RunTaskAsync(() =>
                {
                    try
                    {
                        Frames[dispatcher].HandleAuthFailure();
                    }
                    catch(Exception e)
                    {
                        Logger.LogError("DispatchHandleAuthFailure failed: {0}\n{1}", e.Message, e.StackTrace);
                    }
                }));
            }
            Task.WaitAll(operations.ToArray());
        }

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

        internal void DispatchAddOrUpdateConversations(List<SignalConversation> newConversations)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                operations.Add(dispatcher.RunTaskAsync(() =>
                {
                    foreach (var contact in newConversations)
                    {
                        Frames[dispatcher].AddOrUpdateConversation(contact, null);
                    }
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
            List<TaskCompletionSource<AppendResult>> operations = new List<TaskCompletionSource<AppendResult>>();
            foreach (var dispatcher in Frames.Keys)
            {
                TaskCompletionSource<AppendResult> b = new TaskCompletionSource<AppendResult>();
                var a = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    b.SetResult(Frames[dispatcher].HandleMessage(message, conversation));
                });
                operations.Add(b);
            }
            SignalMessageEvent?.Invoke(this, new SignalMessageEventArgs(message, Events.SignalMessageType.NormalMessage));
            if (message.Author != null)
            {
                bool wasInstantlyRead = false;
                foreach (var b in operations)
                {
                    AppendResult result = b.Task.Result;
                    if (result != null)
                    {
                        SignalDBContext.UpdateMessageRead(result.Index, conversation);
                        DispatchMessageRead(result.Index + 1, conversation).Wait();
                        wasInstantlyRead = true;
                        break;
                    }
                }
                if (!wasInstantlyRead)
                {
                    DispatchHandleUnreadMessage(message);
                }
            }
        }

        internal void DispatchHandleUnreadMessage(SignalMessage message)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                operations.Add(dispatcher.RunTaskAsync(() =>
                {
                    Frames[dispatcher].HandleUnreadMessage(message);
                }));
            }
            Task.WaitAll(operations.ToArray());
        }

        internal async Task DispatchMessageRead(long unreadMarkerIndex, SignalConversation conversation)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                operations.Add(dispatcher.RunTaskAsync(() =>
                {
                    Frames[dispatcher].HandleMessageRead(unreadMarkerIndex, conversation);
                }));
            }
            foreach (var waitHandle in operations)
            {
                await waitHandle;
            }
            Task.WaitAll(operations.ToArray());
        }

        private async Task DispatchCallIncomingIceUpdate(SignalServiceEnvelope envelope, List<IceUpdateMessage> iceUpdateMessages)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                operations.Add(dispatcher.RunTaskAsync(() =>
                {
                    Frames[dispatcher].HandleCallIceUpdatesMessage(envelope, iceUpdateMessages);
                }));
            }
            foreach (var waitHandle in operations)
            {
                await waitHandle;
            }
            Task.WaitAll(operations.ToArray());
        }

        internal async Task DispatchCallOfferMessage(SignalServiceEnvelope envelope, OfferMessage offerMessage)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                operations.Add(dispatcher.RunTaskAsync(() =>
                {
                    Frames[dispatcher].HandleCallOfferMessage(envelope, offerMessage);
                }));
            }
            foreach (var waitHandle in operations)
            {
                await waitHandle;
            }
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

        /// <summary>
        /// Initializes the websocket connection handling. Must not not be called on a UI thread. Must not be called on a task which holds the handle lock.
        /// </summary>
        private void InitNetwork()
        {
            try
            {
                MessageReceiver = new SignalServiceMessageReceiver(CancelSource.Token, LibUtils.ServiceConfiguration, new StaticCredentialsProvider(Store.Username, Store.Password, Store.SignalingKey, (int)Store.DeviceId), LibUtils.USER_AGENT, null);
                Pipe = MessageReceiver.CreateMessagePipe();
                MessageSender = new SignalServiceMessageSender(CancelSource.Token, LibUtils.ServiceConfiguration, Store.Username, Store.Password, (int)Store.DeviceId, new Store(), Pipe, null, LibUtils.USER_AGENT);
                IncomingMessagesTask = Task.Factory.StartNew(() => new IncomingMessages(CancelSource.Token, Pipe, MessageReceiver).HandleIncomingMessages(), TaskCreationOptions.LongRunning);
                OutgoingMessages = new OutgoingMessages(CancelSource.Token, MessageSender, this);
                OutgoingMessagesTask = Task.Factory.StartNew(() => OutgoingMessages.HandleOutgoingMessages(), TaskCreationOptions.LongRunning);
            }
            catch(Exception e)
            {
                Logger.LogError("InitNetwork failed: {0}\n{1}", e.Message, e.StackTrace);
                HandleAuthFailure();
            }
        }

        /// <summary>
        /// Dispatches the auth failure to all frontends and resets the frontend dict. Must not be called on a UI thread. Must not be called on a task which holds the handle lock.
        /// </summary>
        private void HandleAuthFailure()
        {
            Logger.LogTrace("HandleAuthFailure() locking");
            SemaphoreSlim.Wait(CancelSource.Token);
            try
            {
                LikelyHasValidStore = false;
                Running = false;
                CancelSource.Cancel();
                DispatchHandleAuthFailure();
                Frames.Clear();
                Frames.Add(MainWindowDispatcher, MainWindow);
            }
            finally
            {
                SemaphoreSlim.Release();
                Logger.LogTrace("HandleAuthFailure() released");
            }
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
                        return await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(@"Attachments\" + attachment.Id + ".cipher", CreationCollisionOption.ReplaceExisting);
                    }).Result;
                    BackgroundDownloader downloader = new BackgroundDownloader();
                    downloader.SetRequestHeader("Content-Type", "application/octet-stream");
                    // this is the recommended way to call CreateDownload
                    // see https://docs.microsoft.com/en-us/uwp/api/windows.networking.backgroundtransfer.backgrounddownloader#Methods
                    DownloadOperation download = downloader.CreateDownload(new Uri(RetrieveAttachmentUrl(attachmentPointer)), tmpDownload);
                    attachment.Guid = download.Guid.ToString();
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
                StorageFile plaintextFile = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(@"Attachments\" + attachment.Id + ".plain", CreationCollisionOption.ReplaceExisting);
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

        private string RetrieveAttachmentUrl(SignalServiceAttachmentPointer pointer)
        {
            return MessageReceiver.RetrieveAttachmentDownloadUrl(pointer);
        }

        private void DecryptAttachment(SignalServiceAttachmentPointer pointer, Stream ciphertextFileStream, Stream plaintextFileStream)
        {
            byte[] buf = new byte[32];
            Stream s = AttachmentCipherInputStream.CreateFor(ciphertextFileStream, pointer.Size != null ? pointer.Size.Value : 0, pointer.Key, pointer.Digest);
            s.CopyTo(plaintextFileStream);
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
                        if (!Downloads.ContainsKey(attachment.Id))
                        {
                            Logger.LogInformation("Creating attach task for attachment {0} ({1})", attachment.Id, download.Guid);
                            Downloads.Add(attachment.Id, download);
                            var t = Task.Run(async () =>
                            {
                                await download.AttachAsync();
                                await HandleSuccessfullDownload(attachment, download.ResultFile, download);
                            });
                        }
                        else
                        {
                            Logger.LogInformation("Attachment {0} ({1}) already has a running task", attachment.Id, download.Guid);
                        }
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
