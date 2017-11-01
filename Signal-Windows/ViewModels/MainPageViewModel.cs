using GalaSoft.MvvmLight;
using libsignalservice;
using libsignalservice.push.exceptions;
using libsignalservice.util;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using Signal_Windows.Controls;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Signal_Windows.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using static libsignalservice.SignalServiceMessagePipe;

namespace Signal_Windows.ViewModels
{
    public partial class MainPageViewModel : ViewModelBase, IMessagePipeCallback
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<MainPageViewModel>();
        private Visibility _ThreadVisibility = Visibility.Collapsed;

        public Visibility ThreadVisibility
        {
            get { return _ThreadVisibility; }
            set { _ThreadVisibility = value; RaisePropertyChanged(nameof(ThreadVisibility)); }
        }

        private Visibility _WelcomeVisibility;

        public Visibility WelcomeVisibility
        {
            get { return _WelcomeVisibility; }
            set { _WelcomeVisibility = value; RaisePropertyChanged(nameof(WelcomeVisibility)); }
        }

        private bool _ThreadListAlignRight;

        public bool ThreadListAlignRight
        {
            get { return _ThreadListAlignRight; }
            set { _ThreadListAlignRight = value; RaisePropertyChanged(nameof(ThreadListAlignRight)); }
        }

        private AsyncLock ActionInProgress = new AsyncLock();
        public MainPage View;
        public SignalConversation SelectedThread;
        private volatile bool Running = false;
        private Task IncomingMessagesTask;
        private Task OutgoingMessagesTask;
        private CancellationTokenSource CancelSource = new CancellationTokenSource();
        private SignalServiceMessagePipe Pipe;
        private SignalServiceMessageSender MessageSender;
        private SignalServiceMessageReceiver MessageReceiver;

        #region Contacts

        public ObservableCollection<SignalConversation> Threads = new ObservableCollection<SignalConversation>();
        private Dictionary<string, SignalConversation> ThreadsDictionary = new Dictionary<string, SignalConversation>();

        public void AddThread(SignalConversation contact)
        {
            // only add a contact to Threads if it isn't already there
            foreach (var thread in Threads)
            {
                if (thread.ThreadId == contact.ThreadId)
                {
                    return;
                }
            }
            Threads.Add(contact);
            ThreadsDictionary[contact.ThreadId] = contact;
        }

        internal async void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            Logger.LogDebug("BackButton_Click() locking");
            using (await ActionInProgress.LockAsync())
            {
                Logger.LogDebug("BackButton_Click() locked");
                SelectedThread = null;
                View.Thread.DisposeCurrentThread();
                ThreadVisibility = Visibility.Collapsed;
                WelcomeVisibility = Visibility.Visible;
                View.SwitchToStyle(View.GetCurrentViewStyle());
                Utils.DisableBackButton(BackButton_Click);
                e.Handled = true;
            }
            Logger.LogDebug("BackButton_Click() released");
        }

        public void UIUpdateThread(SignalConversation thread)
        {
            SignalConversation uiThread = ThreadsDictionary[thread.ThreadId];
            uiThread.CanReceive = thread.CanReceive;
            uiThread.View.UpdateConversationDisplay(thread);
            if (SelectedThread == uiThread)
            {
                View.Thread.Update(thread);
            }
        }

        #endregion Contacts

        public MainPageViewModel()
        {
            App.MainPageActive = true;
        }

        public async Task OnNavigatedTo()
        {
            if (!Running)
            {
                await Init();
            }
        }

        public async Task Init()
        {
            App.MainPageActive = true;
            Logger.LogDebug("Init() locking");
            using (await ActionInProgress.LockAsync(CancelSource.Token))
            {
                Logger.LogDebug("Init() locked");
                Running = true;
                CancelSource = new CancellationTokenSource();
                try
                {
                    await Task.Run(async () =>
                    {
                        SignalDBContext.FailAllPendingMessages();
                        List<SignalContact> contacts = SignalDBContext.GetAllContactsLocked();
                        List<SignalGroup> groups = SignalDBContext.GetAllGroupsLocked();
                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            try
                            {
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
                                                AddThread(contact);
                                            }
                                            else
                                            {
                                                groupsIdx++;
                                                AddThread(group);
                                            }
                                        }
                                        else
                                        {
                                            contactsIdx++;
                                            AddThread(contact);
                                        }
                                    }
                                    else if (groupsIdx < amountGroups)
                                    {
                                        SignalConversation group = groups[groupsIdx];
                                        groupsIdx++;
                                        AddThread(group);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                var line = new StackTrace(e, true).GetFrames()[0].GetFileLineNumber();
                                Logger.LogError("Init() failed in line {0}: {1}\n{2}", line, e.Message, e.StackTrace);
                            }
                        });
                        MessageReceiver = new SignalServiceMessageReceiver(CancelSource.Token, App.ServiceUrls, new StaticCredentialsProvider(App.Store.Username, App.Store.Password, App.Store.SignalingKey, (int)App.Store.DeviceId), App.USER_AGENT);
                        Pipe = MessageReceiver.createMessagePipe();
                        MessageSender = new SignalServiceMessageSender(CancelSource.Token, App.ServiceUrls, App.Store.Username, App.Store.Password, (int)App.Store.DeviceId, new Store(), Pipe, null, App.USER_AGENT);
                        IncomingMessagesTask = Task.Factory.StartNew(HandleIncomingMessages, TaskCreationOptions.LongRunning);
                        OutgoingMessagesTask = Task.Factory.StartNew(HandleOutgoingMessages, TaskCreationOptions.LongRunning);
                    });
                }
                catch (AuthorizationFailedException)
                {
                    Logger.LogError("Init(): OWS server rejected our credentials - redirecting to StartPage");
                    Running = false;
                    CancelSource.Cancel();
                    View.Frame.Navigate(typeof(StartPage));
                }
                catch (Exception e)
                {
                    var line = new StackTrace(e, true).GetFrames()[0].GetFileLineNumber();
                    Logger.LogError("Init() failed in line {0}: {1}\n{2}", line, e.Message, e.StackTrace);
                }
            }
            Logger.LogDebug("Init() released");
        }

        public async Task OnNavigatingFrom()
        {
            SelectedThread = null;
            View.Thread.DisposeCurrentThread();
        }

        public async Task Shutdown()
        {
            Logger.LogDebug("Shutdown() locking");
            using (await ActionInProgress.LockAsync())
            {
                Running = false;
                App.MainPageActive = false;
                Logger.LogDebug("Shutdown() locked");
                CancelSource.Cancel();
                await IncomingMessagesTask;
                await OutgoingMessagesTask;
            }
            Logger.LogDebug("Shutdown() released");
        }

        internal async void ContactsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1 && SelectedThread != e.AddedItems[0])
            {
                try
                {
                    Logger.LogDebug("ContactsList_SelectionChanged() locking");
                    using (await ActionInProgress.LockAsync())
                    {
                        Logger.LogDebug("ContactsList_SelectionChanged() locked");
                        WelcomeVisibility = Visibility.Collapsed;
                        ThreadVisibility = Visibility.Visible;
                        SelectedThread = (SignalConversation)e.AddedItems[0];
                        View.Thread.Load(SelectedThread);
                        View.SwitchToStyle(View.GetCurrentViewStyle());
                    }
                    Logger.LogDebug("ContactsList_SelectionChanged() released");
                }
                catch (Exception ex)
                {
                    var line = new StackTrace(ex, true).GetFrames()[0].GetFileLineNumber();
                    Logger.LogError("ContactsList_SelectionChanged() failed in line {0}: {1}\n{2}", line, ex.Message, ex.StackTrace);
                }
            }
        }

        internal async Task TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                TextBox t = (TextBox)sender;
                await SendMessageButton_Click(t);
            }
        }

        private async Task<bool> SendMessage(string messageText)
        {
            Logger.LogTrace("SendMessage()");
            try
            {
                if (messageText != string.Empty)
                {
                    var now = Util.CurrentTimeMillis();
                    SignalMessage message = new SignalMessage()
                    {
                        Author = null,
                        ComposedTimestamp = now,
                        Content = new SignalMessageContent() { Content = messageText },
                        ThreadId = SelectedThread.ThreadId,
                        ReceivedTimestamp = now,
                        Direction = SignalMessageDirection.Outgoing,
                        Read = true,
                        Type = SignalMessageType.Normal
                    };
                    Logger.LogTrace("SendMessage() locking");
                    using (await ActionInProgress.LockAsync())
                    {
                        Logger.LogTrace("SendMessage() locked");

                        /* update in-memory data */
                        SelectedThread.MessagesCount += 1;
                        SelectedThread.UnreadCount = 0;
                        SelectedThread.LastMessage = message;
                        SelectedThread.LastSeenMessageIndex = SelectedThread.MessagesCount;
                        SelectedThread.View.UpdateConversationDisplay(SelectedThread);
                        var container = new SignalMessageContainer(message, (int)SelectedThread.MessagesCount - 1);
                        View.Thread.Append(container, true);
                        MoveThreadToTop(SelectedThread);

                        /* save to disk */
                        await Task.Run(() =>
                        {
                            SignalDBContext.SaveMessageLocked(message);
                        });

                        /* add to OutgoingCache */
                        View.Thread.AddToOutgoingMessagesCache(container);

                        /* send */
                        OutgoingQueue.Add(message);
                    }
                    Logger.LogTrace("SendMessage() released");
                }
                return true;
            }
            catch (Exception ex)
            {
                var line = new StackTrace(ex, true).GetFrames()[0].GetFileLineNumber();
                Logger.LogError("SendMessage() failed in line {0}: {1}\n{2}", line, ex.Message, ex.StackTrace);
                return false;
            }
        }

        internal async Task SendMessageButton_Click(TextBox messageTextBox)
        {
            bool sendMessageResult = await SendMessage(messageTextBox.Text);
            if (sendMessageResult)
            {
                messageTextBox.Text = string.Empty;
            }
        }

        public void MoveThreadToTop(SignalConversation thread)
        {
            int n = Threads.IndexOf(thread);
            if (n > 0)
            {
                bool selected = false;
                if (SelectedThread == thread)
                {
                    selected = true;
                }
                Threads.Move(Threads.IndexOf(thread), 0);
                if (selected)
                {
                    View.ReselectTop();
                }
            }
        }

        #region UIThread

        public async Task UIHandleIncomingMessage(SignalMessage message)
        {
            Logger.LogTrace("UIHandleIncomingMessage() locking");
            using (await ActionInProgress.LockAsync())
            {
                Logger.LogTrace("UIHandleIncomingMessage() locked");
                var thread = ThreadsDictionary[message.ThreadId];
                uint unreadCount = thread.UnreadCount;
                if (SelectedThread == thread)
                {
                    message.Read = true;
                }
                await Task.Run(() =>
                {
                    SignalDBContext.SaveMessageLocked(message);
                });
                thread.MessagesCount += 1;
                if (SelectedThread == thread)
                {
                    var container = new SignalMessageContainer(message, (int) thread.MessagesCount - 1);
                    View.Thread.Append(container, false);
                    if (message.Direction == SignalMessageDirection.Synced)
                    {
                        View.Thread.AddToOutgoingMessagesCache(container);
                        unreadCount = 0;
                        thread.LastSeenMessageIndex = thread.MessagesCount;
                    }
                    else
                    {
                        //TODO don't increase unread if we did scroll automatically, and mark the message as seen
                        unreadCount++;
                    }
                }
                else
                {
                    if (message.Direction == SignalMessageDirection.Incoming)
                    {
                        unreadCount++;
                    }
                    else if (message.Direction == SignalMessageDirection.Synced)
                    {
                        unreadCount = 0;
                        thread.LastSeenMessageIndex = thread.MessagesCount;
                    }
                }
                thread.UnreadCount = unreadCount;
                thread.LastActiveTimestamp = message.ReceivedTimestamp;
                thread.LastMessage = message;
                thread.View.UpdateConversationDisplay(thread);
                MoveThreadToTop(thread);
            }
            Logger.LogTrace("UIHandleIncomingMessage() locked");
        }

        public void UIUpdateMessageBox(SignalMessage updatedMessage)
        {
            if (SelectedThread != null && SelectedThread.ThreadId == updatedMessage.ThreadId)
            {
                View.Thread.UpdateMessageBox(updatedMessage);
            }
        }

        public async Task UIHandleIdentityKeyChange(string number)
        {
            Logger.LogTrace("UIHandleIncomingMessage() locking");
            using (await ActionInProgress.LockAsync())
            {
                Logger.LogTrace("UIHandleIncomingMessage() locked");
                var messages = SignalDBContext.InsertIdentityChangedMessages(number);
                foreach (var message in messages)
                {
                    var thread = ThreadsDictionary[message.ThreadId];
                    thread.MessagesCount += 1;
                    if (SelectedThread != null && SelectedThread.ThreadId == message.ThreadId)
                    {
                        var container = new SignalMessageContainer(message, (int) thread.MessagesCount - 1);
                        View.Thread.Append(container, false);
                    }
                    thread.LastMessage = message;
                    thread.View.UpdateConversationDisplay(thread);
                }
            }
            Logger.LogTrace("UIHandleIncomingMessage() released");
        }

        #endregion UIThread
    }
}