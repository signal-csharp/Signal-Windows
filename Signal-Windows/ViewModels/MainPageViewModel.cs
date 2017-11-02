using GalaSoft.MvvmLight;
using libsignalservice;
using libsignalservice.push.exceptions;
using libsignalservice.util;
using Nito.AsyncEx;
using Signal_Windows.Controls;
using Signal_Windows.Lib;
using Signal_Windows.Lib.Constants;
using Signal_Windows.Lib.Models;
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
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using System.Linq;
using static libsignalservice.SignalServiceMessagePipe;

namespace Signal_Windows.ViewModels
{
    public partial class MainPageViewModel : ViewModelBase, IMessagePipeCallback
    {
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

        public async Task AddThread(SignalConversation contact)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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
            });
        }

        internal async void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            Debug.WriteLine("MPVMBack lock wait");
            using (await ActionInProgress.LockAsync())
            {
                Debug.WriteLine("MPVMBack lock grabbed");
                SelectedThread = null;
                View.Thread.DisposeCurrentThread();
                ThreadVisibility = Visibility.Collapsed;
                WelcomeVisibility = Visibility.Visible;
                View.SwitchToStyle(View.GetCurrentViewStyle());
                Utils.DisableBackButton(BackButton_Click);
                e.Handled = true;
            }
            Debug.WriteLine("MPVMBack lock released");
        }

        public async Task UIUpdateThread(SignalConversation thread)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                SignalConversation uiThread = ThreadsDictionary[thread.ThreadId];
                uiThread.CanReceive = thread.CanReceive;
                UpdateThread(thread);
                if (SelectedThread == uiThread)
                {
                    View.Thread.Update(thread);
                }
            });
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
            LibsignalDBContext.IdentityKeyChange += LibsignalDBContext_IdentityKeyChange;
            SignalDBContext.MessageStatusUpdated += SignalDBContext_MessageStatusUpdated;
            SignalDBContext.NewSignalGroup += SignalDBContext_NewSignalGroup;
            SignalDBContext.SignalGroupUpdated += SignalDBContext_SignalGroupUpdated;
            SignalDBContext.NewSignalContact += SignalDBContext_NewSignalContact;
            SignalDBContext.SignalContactUpdated += SignalDBContext_SignalContactUpdated;
        }

        public void OnNavigatingFrom()
        {
            LibsignalDBContext.IdentityKeyChange -= LibsignalDBContext_IdentityKeyChange;
            SignalDBContext.MessageStatusUpdated -= SignalDBContext_MessageStatusUpdated;
            SignalDBContext.NewSignalGroup -= SignalDBContext_NewSignalGroup;
            SignalDBContext.SignalGroupUpdated -= SignalDBContext_SignalGroupUpdated;
            SignalDBContext.NewSignalContact -= SignalDBContext_NewSignalContact;
            SelectedThread = null;
            View.Thread.DisposeCurrentThread();
        }

        private async void LibsignalDBContext_IdentityKeyChange(object sender, Lib.Events.IdentityKeyChangeEventArgs e)
        {
            await UIHandleIdentityKeyChange(e.Number);
        }

        private async void SignalDBContext_MessageStatusUpdated(object sender, Lib.Events.UpdateMessageStatusEventArgs e)
        {
            await UIUpdateMessageBox(e.Message);
        }

        private async void SignalDBContext_NewSignalGroup(object sender, Lib.Events.NewSignalGroupEventArgs e)
        {
            await AddThread(e.Group);
        }

        private async void SignalDBContext_SignalGroupUpdated(object sender, Lib.Events.SignalGroupUpdatedEventArgs e)
        {
            await UIUpdateThread(e.Group);
        }

        private async void SignalDBContext_NewSignalContact(object sender, Lib.Events.NewSignalContactEventArgs e)
        {
            await AddThread(e.Contact);
        }

        private async void SignalDBContext_SignalContactUpdated(object sender, Lib.Events.SignalContactUpdatedEventArgs e)
        {
            await UIUpdateThread(e.Contact);
        }

        public async Task Init()
        {
            if (AppUtils.IsWindowsMobile())
            {
                var statusBarProgressIndicator = StatusBar.GetForCurrentView().ProgressIndicator;
                await statusBarProgressIndicator.ShowAsync();
                statusBarProgressIndicator.Text = "Connecting to Signal...";
            }
            App.MainPageActive = true;
            Debug.WriteLine("Init lock wait");
            using (await ActionInProgress.LockAsync(CancelSource.Token))
            {
                Debug.WriteLine("Init lock grabbed");
                Running = true;
                CancelSource = new CancellationTokenSource();
                try
                {
                    await Task.Run(async () =>
                    {
                        SignalDBContext.FailAllPendingMessages();
                        List<SignalContact> contacts = SignalDBContext.GetAllContactsLocked();
                        List<SignalGroup> groups = SignalDBContext.GetAllGroupsLocked();
                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
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
                                                await AddThread(contact);
                                            }
                                            else
                                            {
                                                groupsIdx++;
                                                await AddThread(group);
                                            }
                                        }
                                        else
                                        {
                                            contactsIdx++;
                                            await AddThread(contact);
                                        }
                                    }
                                    else if (groupsIdx < amountGroups)
                                    {
                                        SignalConversation group = groups[groupsIdx];
                                        groupsIdx++;
                                        await AddThread(group);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine(e.Message);
                                Debug.WriteLine(e.StackTrace);
                            }
                        });
                        MessageReceiver = new SignalServiceMessageReceiver(CancelSource.Token, App.ServiceUrls, new StaticCredentialsProvider(SignalConstants.Store.Username,
                            SignalConstants.Store.Password, SignalConstants.Store.SignalingKey, (int)SignalConstants.Store.DeviceId), App.USER_AGENT);
                        Pipe = MessageReceiver.createMessagePipe();
                        MessageSender = new SignalServiceMessageSender(CancelSource.Token, App.ServiceUrls, SignalConstants.Store.Username, SignalConstants.Store.Password,
                            (int)SignalConstants.Store.DeviceId, new Store(), Pipe, null, App.USER_AGENT);
                        IncomingMessagesTask = Task.Factory.StartNew(HandleIncomingMessages, TaskCreationOptions.LongRunning);
                        OutgoingMessagesTask = Task.Factory.StartNew(HandleOutgoingMessages, TaskCreationOptions.LongRunning);
                    });
                }
                catch (AuthorizationFailedException)
                {
                    Debug.WriteLine("OWS server rejected our credentials - redirecting to StartPage");
                    Running = false;
                    CancelSource.Cancel();
                    View.Frame.Navigate(typeof(StartPage));
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine(e.StackTrace);
                }
                finally
                {
                    if (AppUtils.IsWindowsMobile())
                    {
                        var statusBarProgressIndicator = StatusBar.GetForCurrentView().ProgressIndicator;
                        await statusBarProgressIndicator.HideAsync();
                    }
                }
            }
            Debug.WriteLine("Init lock released");
            if (AppUtils.IsWindowsMobile())
            {
                var statusBarProgressIndicator = StatusBar.GetForCurrentView().ProgressIndicator;
                await statusBarProgressIndicator.HideAsync();
            }
        }

        public async Task Shutdown()
        {
            Debug.WriteLine("Shutdown lock await");
            using (await ActionInProgress.LockAsync())
            {
                Running = false;
                App.MainPageActive = false;
                Debug.WriteLine("Shutdown lock grabbed");
                CancelSource.Cancel();
                await IncomingMessagesTask;
                await OutgoingMessagesTask;
            }
            Debug.WriteLine("Shutdown lock released");
        }

        internal async void ContactsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1 && SelectedThread != e.AddedItems[0])
            {
                try
                {
                    Debug.WriteLine("SelectionChanged lock await");
                    using (await ActionInProgress.LockAsync())
                    {
                        Debug.WriteLine("SelectionChanged lock grabbed");
                        WelcomeVisibility = Visibility.Collapsed;
                        ThreadVisibility = Visibility.Visible;
                        SelectedThread = (SignalConversation)e.AddedItems[0];
                        View.Thread.Load(SelectedThread);
                        View.SwitchToStyle(View.GetCurrentViewStyle());
                    }
                    Debug.WriteLine("SelectionChanged lock released");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    Debug.WriteLine(ex.StackTrace);
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
            Debug.WriteLine("starting sendmessage");
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
                    Debug.WriteLine("keydown lock await");
                    using (await ActionInProgress.LockAsync())
                    {
                        Debug.WriteLine("keydown lock grabbed");

                        /* update in-memory data */
                        SelectedThread.MessagesCount += 1;
                        SelectedThread.UnreadCount = 0;
                        SelectedThread.LastMessage = message;
                        SelectedThread.LastSeenMessageIndex = SelectedThread.MessagesCount;
                        UpdateThread(SelectedThread);
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
                    Debug.WriteLine("keydown lock released");
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
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
            Debug.WriteLine("incoming lock await");
            using (await ActionInProgress.LockAsync())
            {
                Debug.WriteLine("incoming lock grabbed");
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
                UpdateThread(thread);
                MoveThreadToTop(thread);
            }
            Debug.WriteLine("incoming lock released");
        }

        public async Task UIUpdateMessageBox(SignalMessage updatedMessage)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (SelectedThread != null && SelectedThread.ThreadId == updatedMessage.ThreadId)
                {
                    View.Thread.UpdateMessageBox(updatedMessage);
                }
            });
        }

        public async Task UIHandleIdentityKeyChange(string number)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                Debug.WriteLine("IKChange lock await");
                using (await ActionInProgress.LockAsync())
                {
                    Debug.WriteLine("IKChange lock grabbed");
                    var messages = SignalDBContext.InsertIdentityChangedMessages(number);
                    foreach (var message in messages)
                    {
                        var thread = ThreadsDictionary[message.ThreadId];
                        thread.MessagesCount += 1;
                        if (SelectedThread != null && SelectedThread.ThreadId == message.ThreadId)
                        {
                            var container = new SignalMessageContainer(message, (int)thread.MessagesCount - 1);
                            View.Thread.Append(container, false);
                        }
                        thread.LastMessage = message;
                        UpdateThread(thread);
                    }
                }
                Debug.WriteLine("IKChange lock released");
            });
        }

        #endregion UIThread

        private int? GetThreadIndex(SignalConversation conversation)
        {
            for (int i = 0; i < Threads.Count; i++)
            {
                if (conversation.Id == Threads[i].Id)
                {
                    return i;
                }
            }
            return null;
        }

        private void UpdateThread(SignalConversation conversation)
        {
            int? index = GetThreadIndex(conversation);
            if (index.HasValue)
            {
                Threads[index.Value] = conversation;
            }
        }
    }
}