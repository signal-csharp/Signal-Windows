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

namespace Signal_Windows.Lib
{
    public class AppendResult
    {
        public bool WasInstantlyRead { get;  }
        public AppendResult(bool wasInstantlyRead)
        {
            WasInstantlyRead = wasInstantlyRead;
        }
    }

    public interface ISignalFrontend
    {
        void AddOrUpdateConversation(SignalConversation updatedConversation, SignalMessage updateMessage);
        AppendResult HandleMessage(SignalMessage message, SignalConversation updatedConversation);
        void HandleUnreadMessage(SignalMessage message);
        void HandleMessageRead(SignalConversation updatedConversation);
        void HandleIdentitykeyChange(LinkedList<SignalMessage> messages);
        void HandleMessageUpdate(SignalMessage updatedMessage);
        void ReplaceConversationList(List<SignalConversation> conversations);
        Task HandleAuthFailure();
        void HandleAttachmentStatusChanged(SignalAttachment sa);
        void HandleBlockedContacts(List<SignalContact> blockedContacts);
    }

    public interface ISignalLibHandle
    {
        //Frontend API
        SignalStore Store { get; set; }

        void RequestSync();
        Task SendMessage(string messageText, StorageFile attachment, SignalConversation conversation);
        Task SendBlockedMessage();
        Task SetMessageRead(SignalMessage message);
        void ResendMessage(SignalMessage message);
        IEnumerable<SignalMessage> GetMessages(SignalConversation thread, int startIndex, int count);
        Task SaveAndDispatchSignalConversation(SignalConversation updatedConversation, SignalMessage updateMessage);
        void PurgeAccountData();
        Task<bool> Acquire(CoreDispatcher d, ISignalFrontend w);
        Task Reacquire();
        void Release();
        bool AddFrontend(CoreDispatcher d, ISignalFrontend w);
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
                var initNetwork = Task.Run(async () =>
                {
                    await InitNetwork();
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
                await Task.Run(async () =>
                {
                    List<Task> tasks = new List<Task>();
                    foreach (var f in Frames)
                    {
                        var conversations = GetConversations();
                        var taskCompletionSource = new TaskCompletionSource<bool>();
                        await f.Key.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            try
                            {
                                f.Value.ReplaceConversationList(conversations);
                            }
                            catch (Exception e)
                            {
                                Logger.LogError("Reacquire() ReplaceConversationList() failed: {0}\n{1}", e.Message, e.StackTrace);
                            }
                            finally
                            {
                                taskCompletionSource.SetResult(false);
                            }
                        });
                        tasks.Add(taskCompletionSource.Task);
                    }
                    foreach (var t in tasks)
                    {
                        await t;
                    }
                    await RecoverDownloads();
                    Store = LibsignalDBContext.GetSignalStore();
                    if (Store != null)
                    {
                        LikelyHasValidStore = true;
                    }
                });
                if (LikelyHasValidStore)
                {
                    var initNetworkTask = Task.Run(async () =>
                    {
                        await InitNetwork();
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

        public async Task SendMessage(string messageText, StorageFile attachmentStorageFile, SignalConversation conversation)
        {
            await Task.Run(async () =>
            {
                Logger.LogTrace("SendMessage() locking");
                SemaphoreSlim.Wait(CancelSource.Token);
                Logger.LogTrace("SendMessage() locked");
                try
                {
                    var now = Util.CurrentTimeMillis();
                    var attachmentsList = new List<SignalAttachment>();
                    if (attachmentStorageFile != null)
                    {
                        attachmentsList.Add(new SignalAttachment()
                        {
                            ContentType = attachmentStorageFile.ContentType,
                            SentFileName = attachmentStorageFile.Name
                        });
                    }

                    SignalMessage message = new SignalMessage()
                    {
                        Author = null,
                        ComposedTimestamp = now,
                        ExpiresAt = conversation.ExpiresInSeconds,
                        Content = new SignalMessageContent() { Content = messageText },
                        ThreadId = conversation.ThreadId,
                        ReceivedTimestamp = now,
                        Direction = SignalMessageDirection.Outgoing,
                        Read = true,
                        Type = SignalMessageType.Normal,
                        Attachments = attachmentsList,
                        AttachmentsCount = (uint) attachmentsList.Count()
                    };
                    await SaveAndDispatchSignalMessage(message, attachmentStorageFile, conversation);
                    OutgoingQueue.Add(message);
                }
                finally
                {
                    SemaphoreSlim.Release();
                    Logger.LogTrace("SendMessage() released");
                }
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
        public async Task SetMessageRead(SignalMessage message)
        {
            Logger.LogTrace("SetMessageRead() locking");
            await SemaphoreSlim.WaitAsync(CancelSource.Token);
            try
            {
                Logger.LogTrace("SetMessageRead() locked");
                var updatedConversation = SignalDBContext.UpdateMessageRead(message.ComposedTimestamp);
                OutgoingMessages.SendMessage(SignalServiceSyncMessage.ForRead(new List<ReadMessage>() {
                        new ReadMessage(message.Author.ThreadId, message.ComposedTimestamp)
                }));
                await DispatchMessageRead(updatedConversation);
            }
            catch (Exception e)
            {
                Logger.LogError("SetMessageRead() failed: {0}\n{1}", e.Message, e.StackTrace);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
            Logger.LogTrace("SetMessageRead() released");
        }

        public void ResendMessage(SignalMessage message)
        {
            OutgoingQueue.Add(message);
        }

        public IEnumerable<SignalMessage> GetMessages(SignalConversation thread, int startIndex, int count)
        {
            return SignalDBContext.GetMessagesLocked(thread, startIndex, count);
        }

        public async Task SaveAndDispatchSignalConversation(SignalConversation updatedConversation, SignalMessage updateMessage)
        {
            Logger.LogTrace("SaveAndDispatchSignalConversation() locking");
            await SemaphoreSlim.WaitAsync(CancelSource.Token);
            try
            {
                SignalDBContext.InsertOrUpdateConversationLocked(updatedConversation);
                await DispatchAddOrUpdateConversation(updatedConversation, updateMessage);
            }
            catch (Exception e)
            {
                Logger.LogError("SaveAndDispatchSignalConversation() failed: {0}\n{1}", e.Message, e.StackTrace);
            }
            finally
            {
                SemaphoreSlim.Release();
                Logger.LogTrace("SaveAndDispatchSignalConversation() released");
            }
        }

        public async Task SendBlockedMessage()
        {
            List<SignalContact> blockedContacts = SignalDBContext.GetAllContactsLocked().Where(c => c.Blocked).ToList();
            List<string> blockedNumbers = new List<string>();
            foreach (var contact in blockedContacts)
            {
                blockedNumbers.Add(contact.ThreadId);
            }
            var blockMessage = SignalServiceSyncMessage.ForBlocked(new BlockedListMessage(blockedNumbers));
            OutgoingMessages.SendMessage(blockMessage);
            await DispatchHandleBlockedContacts(blockedContacts);
        }
        #endregion

        #region attachment api
        public void StartAttachmentDownload(SignalAttachment sa)
        {
            //TODO lock, check if already downloading, start a new download if not exists
            Task.Run(async () =>
            {
                try
                {
                    Logger.LogTrace("StartAttachmentDownload() locking");
                    SemaphoreSlim.Wait(CancelSource.Token);
                    Logger.LogTrace("StartAttachmentDownload() locked");
                    await TryScheduleAttachmentDownload(sa);
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
        internal async Task DispatchHandleAuthFailure()
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal,async () =>
                {
                    try
                    {
                        await Frames[dispatcher].HandleAuthFailure();
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("DispatchHandleAuthFailure failed: {0}\n{1}", e.Message, e.StackTrace);
                    }
                    finally
                    {
                        taskCompletionSource.SetResult(false);
                    }
                });
                operations.Add(taskCompletionSource.Task);
            }
            foreach (var t in operations)
            {
                await t;
            }
        }

        internal async Task SaveAndDispatchSignalMessage(SignalMessage message, StorageFile attachmentStorageFile, SignalConversation conversation)
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
            if (attachmentStorageFile != null)
            {
                StorageFolder plaintextFile = await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync(@"Attachments\", CreationCollisionOption.OpenIfExists);
                foreach (var attachment in message.Attachments)
                {
                    Logger.LogTrace(@"Copying attachment to \Attachments\{0}.plain", attachment.Id.ToString());
                    await attachmentStorageFile.CopyAsync(plaintextFile, attachment.Id.ToString() + ".plain", NameCollisionOption.ReplaceExisting);
                }
            }
            await DispatchHandleMessage(message, conversation);
        }

        internal async Task DispatchHandleIdentityKeyChange(LinkedList<SignalMessage> messages)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        Frames[dispatcher].HandleIdentitykeyChange(messages);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("DispatchHandleIdentityKeyChange() dispatch failed: {0}\n{1}", e.Message, e.StackTrace);
                    }
                    finally
                    {
                        taskCompletionSource.SetResult(false);
                    }
                });
                operations.Add(taskCompletionSource.Task);
            }
            foreach (var t in operations)
            {
                await t;
            }
        }

        internal async Task DispatchAddOrUpdateConversations(IList<SignalConversation> newConversations)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        foreach (var contact in newConversations)
                        {
                            Frames[dispatcher].AddOrUpdateConversation(contact, null);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("DispatchAddOrUpdateConversations() dispatch failed: {0}\n{1}", e.Message, e.StackTrace);
                    }
                    finally
                    {
                        taskCompletionSource.SetResult(false);
                    }
                });
                operations.Add(taskCompletionSource.Task);
            }
            foreach (var t in operations)
            {
                await t;
            }
        }

        internal async Task DispatchAddOrUpdateConversation(SignalConversation conversation, SignalMessage updateMessage)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        Frames[dispatcher].AddOrUpdateConversation(conversation, updateMessage);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("DispatchAddOrUpdateConversation() dispatch failed: {0}\n{1}", e.Message, e.StackTrace);
                    }
                    finally
                    {
                        taskCompletionSource.SetResult(false);
                    }
                });
                operations.Add(taskCompletionSource.Task);
            }
            foreach (var t in operations)
            {
                await t;
            }
        }

        internal async Task DispatchHandleMessage(SignalMessage message, SignalConversation conversation)
        {
            List<Task<AppendResult>> operations = new List<Task<AppendResult>>();
            foreach (var dispatcher in Frames.Keys)
            {
                TaskCompletionSource<AppendResult> taskCompletionSource = new TaskCompletionSource<AppendResult>();
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    AppendResult ar = null;
                    try
                    {
                        ar = (Frames[dispatcher].HandleMessage(message, conversation));
                    }
                    catch(Exception e)
                    {
                        Logger.LogError("DispatchHandleMessage() dispatch failed: {0}\n{1}", e.Message, e.StackTrace);
                    }
                    finally
                    {
                        taskCompletionSource.SetResult(ar);
                    }
                });
                operations.Add(taskCompletionSource.Task);
            }
            SignalMessageEvent?.Invoke(this, new SignalMessageEventArgs(message, Events.SignalPipeMessageType.NormalMessage));
            if (message.Author != null)
            {
                bool wasInstantlyRead = false;
                foreach (var b in operations)
                {
                    AppendResult result = await b;
                    if (result != null && result.WasInstantlyRead)
                    {
                        var updatedConversation = SignalDBContext.UpdateMessageRead(message.ComposedTimestamp);
                        await DispatchMessageRead(updatedConversation);
                        wasInstantlyRead = true;
                        break;
                    }
                }
                if (!wasInstantlyRead)
                {
                    await DispatchHandleUnreadMessage(message);
                }
            }
        }

        internal async Task DispatchHandleUnreadMessage(SignalMessage message)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        Frames[dispatcher].HandleUnreadMessage(message);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("DispatchHandleUnreadMessage() dispatch failed: {0}\n{1}", e.Message, e.StackTrace);
                    }
                    finally
                    {
                        taskCompletionSource.SetResult(false);
                    }
                });
                operations.Add(taskCompletionSource.Task);
            }
            foreach (var t in operations)
            {
                await t;
            }
        }

        internal async Task DispatchMessageRead(SignalConversation conversation)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        Frames[dispatcher].HandleMessageRead(conversation);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("DispatchMessageRead() dispatch failed: {0}\n{1}", e.Message, e.StackTrace);
                    }
                    finally
                    {
                        taskCompletionSource.SetResult(false);
                    }
                });
                operations.Add(taskCompletionSource.Task);
            }
            foreach (var waitHandle in operations)
            {
                await waitHandle;
            }
        }

        internal void DispatchPipeEmptyMessage()
        {
            SignalMessageEvent?.Invoke(this, new SignalMessageEventArgs(null, Events.SignalPipeMessageType.PipeEmptyMessage));
        }

        internal async Task HandleMessageSentLocked(SignalMessage msg)
        {
            Logger.LogTrace("HandleMessageSentLocked() locking");
            await SemaphoreSlim.WaitAsync(CancelSource.Token);
            Logger.LogTrace("HandleMessageSentLocked() locked");
            var updated = SignalDBContext.UpdateMessageStatus(msg);
            await DispatchMessageUpdate(updated);
            SemaphoreSlim.Release();
            Logger.LogTrace("HandleMessageSentLocked() released");
        }

        internal async Task DispatchMessageUpdate(SignalMessage msg)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        Frames[dispatcher].HandleMessageUpdate(msg);
                    }
                    catch(Exception e)
                    {
                        Logger.LogError("DispatchMessageUpdate() dispatch failed: {0}\n{1}", e.Message, e.StackTrace);
                    }
                    finally
                    {
                        taskCompletionSource.SetResult(false);
                    }
                });
                operations.Add(taskCompletionSource.Task);
            }
            foreach (var t in operations)
            {
                await t;
            }
        }

        internal async Task HandleOutgoingKeyChangeLocked(string user, string identity)
        {
            Logger.LogTrace("HandleOutgoingKeyChange() locking");
            await SemaphoreSlim.WaitAsync(CancelSource.Token);
            try
            {
                Logger.LogTrace("HandleOutgoingKeyChange() locked");
                await LibsignalDBContext.SaveIdentityLocked(new SignalProtocolAddress(user, 1), identity);
            }
            catch (Exception e)
            {
                Logger.LogError("HandleOutgoingKeyChangeLocked() failed: {0}\n{1}", e.Message, e.StackTrace);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
            Logger.LogTrace("HandleOutgoingKeyChange() released");
        }

        /// <summary>
        /// This will notify all windows of newly blocked numbers.
        /// This does not save to the database.
        /// </summary>
        /// <param name="blockedContacts">The list of blocked contacts</param>
        internal async Task DispatchHandleBlockedContacts(List<SignalContact> blockedContacts)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        Frames[dispatcher].HandleBlockedContacts(blockedContacts);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("DispatchHandleBlockedContacts() dispatch failed: {0}\n{1}", e.Message, e.StackTrace);
                    }
                    finally
                    {
                        taskCompletionSource.SetResult(false);
                    }
                });
                operations.Add(taskCompletionSource.Task);
            }
            foreach (var t in operations)
            {
                await t;
            }
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
        private async Task InitNetwork()
        {
            try
            {
                MessageReceiver = new SignalServiceMessageReceiver(LibUtils.ServiceConfiguration, new StaticCredentialsProvider(Store.Username, Store.Password, Store.SignalingKey, (int)Store.DeviceId), LibUtils.USER_AGENT);
                Pipe = await MessageReceiver.CreateMessagePipe(CancelSource.Token, new SignalWebSocketFactory());
                MessageSender = new SignalServiceMessageSender(CancelSource.Token, LibUtils.ServiceConfiguration, Store.Username, Store.Password, (int)Store.DeviceId, new Store(), Pipe, null, LibUtils.USER_AGENT);
                IncomingMessagesTask = Task.Factory.StartNew(async () => await new IncomingMessages(CancelSource.Token, Pipe, MessageReceiver).HandleIncomingMessages(), TaskCreationOptions.LongRunning);
                OutgoingMessages = new OutgoingMessages(CancelSource.Token, MessageSender, this);
                OutgoingMessagesTask = Task.Factory.StartNew(async () => await OutgoingMessages.HandleOutgoingMessages(), TaskCreationOptions.LongRunning);
            }
            catch(Exception e)
            {
                Logger.LogError("InitNetwork failed: {0}\n{1}", e.Message, e.StackTrace);
                await HandleAuthFailure();
            }
        }

        /// <summary>
        /// Dispatches the auth failure to all frontends and resets the frontend dict. Must not be called on a UI thread. Must not be called on a task which holds the handle lock.
        /// </summary>
        private async Task HandleAuthFailure()
        {
            Logger.LogTrace("HandleAuthFailure() locking");
            SemaphoreSlim.Wait(CancelSource.Token);
            try
            {
                LikelyHasValidStore = false;
                Running = false;
                CancelSource.Cancel();
                await DispatchHandleAuthFailure();
                Frames.Clear();
                Frames.Add(MainWindowDispatcher, MainWindow);
            }
            finally
            {
                SemaphoreSlim.Release();
                Logger.LogTrace("HandleAuthFailure() released");
            }
        }

        private async Task TryScheduleAttachmentDownload(SignalAttachment attachment)
        {
            if (Downloads.Count < 100)
            {
                if (attachment.Status != SignalAttachmentStatus.Finished && !Downloads.ContainsKey(attachment.Id))
                {
                    SignalServiceAttachmentPointer attachmentPointer = attachment.ToAttachmentPointer();
                    IStorageFolder localFolder = ApplicationData.Current.LocalFolder;
                    IStorageFile tmpDownload = await Task.Run(async () =>
                    {
                        return await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(@"Attachments\" + attachment.Id + ".cipher", CreationCollisionOption.ReplaceExisting);
                    });
                    BackgroundDownloader downloader = new BackgroundDownloader();
                    downloader.SetRequestHeader("Content-Type", "application/octet-stream");
                    // this is the recommended way to call CreateDownload
                    // see https://docs.microsoft.com/en-us/uwp/api/windows.networking.backgroundtransfer.backgrounddownloader#Methods
                    DownloadOperation download = downloader.CreateDownload(new Uri(await MessageReceiver.RetrieveAttachmentDownloadUrl(CancelSource.Token, attachmentPointer)), tmpDownload);
                    attachment.Guid = download.Guid.ToString();
                    SignalDBContext.UpdateAttachmentGuid(attachment);
                    Downloads.Add(attachment.Id, download);
                    var downloadSuccessfulHandler = Task.Run(async () =>
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
                await DispatchAttachmentStatusChanged(download, attachment);
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

        private async Task DispatchAttachmentStatusChanged(DownloadOperation op, SignalAttachment attachment)
        {
            try
            {
                List<Task> operations = new List<Task>();
                foreach (var dispatcher in Frames.Keys)
                {
                    var taskCompletionSource = new TaskCompletionSource<bool>();
                    await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        try
                        {
                            Frames[dispatcher].HandleAttachmentStatusChanged(attachment);
                        }
                        catch (Exception e)
                        {
                            Logger.LogError("DispatchAttachmentStatusChanged() dispatch failed: {0}\n{1}", e.Message, e.StackTrace);
                        }
                        finally
                        {
                            taskCompletionSource.SetResult(false);
                        }
                    });
                    operations.Add(taskCompletionSource.Task);
                }
                foreach (var t in operations)
                {
                    await t;
                }
            }
            catch(Exception e)
            {
                Logger.LogError("DispatchAttachmentStatusChanged encountered an error: {0}\n{1}", e.Message, e.StackTrace);
            }
        }
        #endregion
    }
}
