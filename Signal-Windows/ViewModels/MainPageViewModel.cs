using GalaSoft.MvvmLight;
using libsignalservice;
using libsignalservice.util;
using Nito.AsyncEx;
using Signal_Windows.Models;
using Signal_Windows.Storage;
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
    public partial class MainPageViewModel : ViewModelBase, MessagePipeCallback
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
        private volatile bool Running = true;
        private Task IncomingMessagesTask;
        private Task OutgoingMessagesTask;
        private CancellationTokenSource CancelSource;
        private SignalServiceMessagePipe Pipe;
        private SignalServiceMessageSender MessageSender;
        private SignalServiceMessageReceiver MessageReceiver;

        #region Contacts

        public ObservableCollection<SignalConversation> Threads = new ObservableCollection<SignalConversation>();
        private Dictionary<string, SignalConversation> ThreadsDictionary = new Dictionary<string, SignalConversation>();

        public void AddThread(SignalConversation contact)
        {
            Threads.Add(contact);
            ThreadsDictionary[contact.ThreadId] = contact;
        }

        internal async void BackButton_Click(object sender, BackRequestedEventArgs e)
        {
            using (await ActionInProgress.LockAsync())
            {
                SelectedThread = null;
                View.Thread.DisposeCurrentThread();
                ThreadVisibility = Visibility.Collapsed;
                WelcomeVisibility = Visibility.Visible;
                View.SwitchToStyle(View.GetCurrentViewStyle());
                Utils.DisableBackButton(BackButton_Click);
            }
        }

        public void UIUpdateThread(SignalConversation thread)
        {
            SignalConversation uiThread = ThreadsDictionary[thread.ThreadId];
            uiThread.CanReceive = thread.CanReceive;
            uiThread.View.Update(thread);
            if (SelectedThread == uiThread)
            {
                View.Thread.Update(thread);
            }
        }

        #endregion Contacts

        public MainPageViewModel()
        {
            App.MainPageActive = true;
            Init();
        }

        public void Init()
        {
            CancelSource = new CancellationTokenSource();
            var l = ActionInProgress.Lock(CancelSource.Token);
            try
            {
                Task.Run(async () =>
                {
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
                            Debug.WriteLine(e.Message);
                            Debug.WriteLine(e.StackTrace);
                        }
                    });
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        try
                        {
                            MessageReceiver = new SignalServiceMessageReceiver(CancelSource.Token, App.ServiceUrls, new StaticCredentialsProvider(App.Store.Username, App.Store.Password, App.Store.SignalingKey, (int)App.Store.DeviceId), App.USER_AGENT);
                            Pipe = MessageReceiver.createMessagePipe();
                            MessageSender = new SignalServiceMessageSender(CancelSource.Token, App.ServiceUrls, App.Store.Username, App.Store.Password, (int)App.Store.DeviceId, new Store(), Pipe, null, App.USER_AGENT);
                            IncomingMessagesTask = Task.Factory.StartNew(HandleIncomingMessages, TaskCreationOptions.LongRunning);
                            OutgoingMessagesTask = Task.Factory.StartNew(HandleOutgoingMessages, TaskCreationOptions.LongRunning);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.Message);
                            Debug.WriteLine(e.StackTrace);
                        }
                    });
                    l.Dispose();
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);
            }
        }

        public async Task Shutdown()
        {
            Running = false;
            App.MainPageActive = false;
            var l = await ActionInProgress.LockAsync();
            CancelSource.Cancel();
            await IncomingMessagesTask;
            await OutgoingMessagesTask;
        }

        internal async void ContactsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1 && SelectedThread != e.AddedItems[0])
            {
                try
                {
                    using (await ActionInProgress.LockAsync())
                    {
                        WelcomeVisibility = Visibility.Collapsed;
                        ThreadVisibility = Visibility.Visible;
                        SelectedThread = (SignalConversation)e.AddedItems[0];
                        await View.Thread.Load(SelectedThread);
                        View.SwitchToStyle(View.GetCurrentViewStyle());
                        await Task.Run(() =>
                        {
                            SignalDBContext.ClearUnreadLocked(SelectedThread.ThreadId, this);
                        });
                        UIResetRead(SelectedThread.ThreadId);
                        View.Thread.ScrollToBottom();
                    }
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
            try
            {
                if (e.Key == VirtualKey.Enter)
                {
                    TextBox t = (TextBox)sender;
                    if (t.Text != "")
                    {
                        var text = t.Text;
                        t.Text = "";
                        var now = Util.CurrentTimeMillis();
                        SignalMessage message = new SignalMessage()
                        {
                            Author = null,
                            ComposedTimestamp = now,
                            Content = new SignalMessageContent() { Content = text },
                            ThreadId = SelectedThread.ThreadId,
                            ReceivedTimestamp = now,
                            Direction = 0
                        };
                        using (await ActionInProgress.LockAsync())
                        {
                            View.Thread.Append(message);
                            View.Thread.ScrollToBottom();
                            MoveThreadToTop(SelectedThread);
                            await Task.Run(() =>
                            {
                                SignalDBContext.SaveMessageLocked(message);
                            });
                            if (SelectedThread != null && SelectedThread.ThreadId == message.ThreadId)
                            {
                                View.Thread.AddToCache(message);
                            }
                            OutgoingQueue.Add(message);
                            var after = Util.CurrentTimeMillis();
                            Debug.WriteLine("ms until out queue: " + (after - now));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
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
            using (await ActionInProgress.LockAsync())
            {
                var thread = ThreadsDictionary[message.ThreadId];
                uint unread = thread.UnreadCount;
                await Task.Run(() =>
                {
                    SignalDBContext.SaveMessageLocked(message);
                });
                if (SelectedThread == thread)
                {
                    View.Thread.Append(message);
                    View.Thread.ScrollToBottom();
                    if (message.Direction == SignalMessageDirection.Synced)
                    {
                        View.Thread.AddToCache(message);
                    }
                }
                else
                {
                    if (message.Direction == SignalMessageDirection.Incoming)
                    {
                        unread++;
                        await Task.Run(() =>
                        {
                            SignalDBContext.UpdateConversationLocked(message.ThreadId, unread);
                        });
                    }
                    else if (message.Direction == SignalMessageDirection.Synced)
                    {
                        unread = 0;
                        await Task.Run(() =>
                        {
                            SignalDBContext.UpdateConversationLocked(message.ThreadId, unread);
                        });
                    }
                }
                thread.UnreadCount = unread;
                thread.LastActiveTimestamp = message.ReceivedTimestamp;
                ThreadsDictionary[message.ThreadId].View.UnreadCount = unread;
                MoveThreadToTop(thread);
            }
        }

        public void UIResetRead(string threadId)
        {
            ThreadsDictionary[threadId].UnreadCount = 0;
            ThreadsDictionary[threadId].View.UnreadCount = 0;
        }

        public void UIUpdateMessageBox(SignalMessage updatedMessage)
        {
            if (SelectedThread != null && SelectedThread.ThreadId == updatedMessage.ThreadId)
            {
                View.Thread.UpdateMessageBox(updatedMessage);
            }
        }

        #endregion UIThread
    }
}