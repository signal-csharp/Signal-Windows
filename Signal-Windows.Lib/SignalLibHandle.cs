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

namespace Signal_Windows.Lib
{
    public interface ISignalWindow
    {
        void AddOrUpdateConversation(SignalConversation conversation);
        void HandleMessage(SignalMessage message);
        void HandleIdentitykeyChange(LinkedList<SignalMessage> messages);
        void UpdateMessageBox(SignalMessage updatedMessage);
    }


    public class SignalLibHandle
    {
        public static SignalLibHandle Instance;
        public SignalStore Store;
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<SignalLibHandle>();
        public SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);
        private bool Headless;
        private CancellationTokenSource CancelSource = new CancellationTokenSource();
        private Dictionary<CoreDispatcher, ISignalWindow> Frames = new Dictionary<CoreDispatcher, ISignalWindow>();
        private Task IncomingMessagesTask;
        private Task OutgoingMessagesTask;
        private SignalServiceMessagePipe Pipe;
        private SignalServiceMessageSender MessageSender;
        private SignalServiceMessageReceiver MessageReceiver;
        public BlockingCollection<SignalMessage> OutgoingQueue = new BlockingCollection<SignalMessage>(new ConcurrentQueue<SignalMessage>());

        #region frontend api
        public SignalLibHandle(bool headless)
        {
            Headless = headless;
            Instance = this;
        }

        public List<SignalConversation> AddWindow(CoreDispatcher d, ISignalWindow w)
        {
            Logger.LogTrace("AddWindow() locking");
            SemaphoreSlim.Wait(CancelSource.Token);
            Logger.LogTrace("AddWindow() locked");
            Logger.LogInformation("Registering window of dispatcher {0}", w.GetHashCode());
            Frames.Add(d, w);
            var conversations = GetConversations();
            SemaphoreSlim.Release();
            Logger.LogTrace("AddWindow() released");
            return conversations;
        }

        public void RemoveWindow(CoreDispatcher d)
        {
            Logger.LogTrace("RemoveWindow() locking");
            SemaphoreSlim.Wait(CancelSource.Token);
            Logger.LogTrace("RemoveWindow() locked");
            Logger.LogInformation("Unregistering window of dispatcher {0}", d.GetHashCode());
            Frames.Remove(d);
            SemaphoreSlim.Release();
            Logger.LogTrace("RemoveWindow() released");
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

        public void Acquire()
        {
            //TODO ensure not acquired, dispatch view information
            Logger.LogTrace("Acquire() locking");
            CancelSource = new CancellationTokenSource();
            SemaphoreSlim.Wait(CancelSource.Token);
            LibUtils.Lock();
            Logger.LogDebug("Acquire() locked (global and local)");
            Instance = this;
            SignalDBContext.Migrate();
            LibsignalDBContext.Migrate();
            SignalDBContext.FailAllPendingMessages();
            Store = LibsignalDBContext.GetSignalStore();
            InitNetwork();
            Logger.LogTrace("Acquire() releasing");
            SemaphoreSlim.Release();
        }

        public void Release()
        {
            //TODO invalidate view information
            Logger.LogTrace("Release() locking");
            SemaphoreSlim.Wait(CancelSource.Token);
            CancelSource.Cancel();
            IncomingMessagesTask?.Wait();
            OutgoingMessagesTask?.Wait();
            Instance = null;
            Logger.LogTrace("Release() releasing (global and local)");
            LibUtils.Unlock();
            SemaphoreSlim.Release();
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

        public async void UpdateConversation(SignalConversation updatedConversation)
        {
            await Task.Run(async () =>
            {
                Logger.LogTrace("UpdateConversation() locking");
                await SemaphoreSlim.WaitAsync(CancelSource.Token);
                SemaphoreSlim.Release();
                Logger.LogTrace("UpdateConversation() released");
            });
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
            DispatchAddOrUpdateConversation(conversation); //first update the conversation (including MessagesCount)...
            DispatchHandleMessage(message); //then pass the message to all windows
        }

        internal void DispatchHandleIdentityKeyChange(LinkedList<SignalMessage> messages)
        {
            List<Task> operations = new List<Task>(); ;
            foreach (var dispatcher in Frames.Keys)
            {
                operations.Add(dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Frames[dispatcher].HandleIdentitykeyChange(messages);
                }).AsTask());
            }
            Task.WaitAll(operations.ToArray());
        }

        internal void DispatchAddOrUpdateConversation(SignalConversation conversation)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                operations.Add(dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Frames[dispatcher].AddOrUpdateConversation(conversation);
                }).AsTask());
            }
            Task.WaitAll(operations.ToArray());
        }

        internal void DispatchHandleMessage(SignalMessage message)
        {
            List<Task> operations = new List<Task>();
            foreach (var dispatcher in Frames.Keys)
            {
                operations.Add(dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Frames[dispatcher].HandleMessage(message);
                }).AsTask());
            }
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
                operations.Add(dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Frames[dispatcher].UpdateMessageBox(msg);
                }).AsTask());
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
