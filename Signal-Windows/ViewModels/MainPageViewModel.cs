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
        private Visibility _ThreadVisibility;

        public Visibility ThreadVisibility
        {
            get { return _ThreadVisibility; }
            set { _ThreadVisibility = value; RaisePropertyChanged(nameof(ThreadVisibility)); }
        }

        private bool _ThreadListAlignRight;

        public bool ThreadListAlignRight
        {
            get { return _ThreadListAlignRight; }
            set { _ThreadListAlignRight = value; RaisePropertyChanged(nameof(ThreadListAlignRight)); }
        }

        private AsyncLock ActionInProgress = new AsyncLock();
        public MainPage View;
        public SignalThread SelectedThread;
        private volatile bool Running = true;
        private Task IncomingMessagesTask;
        private Task OutgoingMessagesTask;
        private CancellationTokenSource CancelSource;
        private SignalServiceMessagePipe Pipe;
        private SignalServiceMessageSender MessageSender;
        private SignalServiceMessageReceiver MessageReceiver;

        #region Contacts

        public ObservableCollection<SignalThread> Threads = new ObservableCollection<SignalThread>();
        private Dictionary<string, SignalThread> ThreadsDictionary = new Dictionary<string, SignalThread>();

        public void AddThread(SignalThread contact)
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
                View.SwitchToStyle(View.GetCurrentViewStyle());
                Utils.DisableBackButton(BackButton_Click);
            }
        }

        public void UIUpdateThread(SignalThread thread)
        {
            SignalThread uiThread = ThreadsDictionary[thread.ThreadId];
            uiThread.View.Update(thread);
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
                                    SignalThread contact = contacts[contactsIdx];
                                    if (groupsIdx < amountGroups)
                                    {
                                        SignalThread group = groups[groupsIdx];
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
                                    SignalThread group = groups[groupsIdx];
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
                        SelectedThread = (SignalThread)e.AddedItems[0];
                        await View.Thread.Load(SelectedThread);
                        View.SwitchToStyle(View.GetCurrentViewStyle());
                        await Task.Run(() =>
                        {
                            SignalDBContext.ClearUnreadLocked(SelectedThread.ThreadId);
                        });
                        ThreadsDictionary[SelectedThread.ThreadId].Unread = 0;
                        ThreadsDictionary[SelectedThread.ThreadId].View.UnreadCount = 0;
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
                            Type = 0
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

        public void MoveThreadToTop(SignalThread thread)
        {
            var n = Threads.IndexOf(thread);
            bool selected = false;
            if (SelectedThread == thread)
            {
                selected = true;
            }
            Threads.RemoveAt(n);
            Threads.Insert(0, thread);
            if (selected)
            {
                View.ReselectTop();
            }
        }

        #region UIThread

        public async Task UIHandleIncomingMessage(SignalMessage message)
        {
            using (await ActionInProgress.LockAsync())
            {
                var thread = ThreadsDictionary[message.ThreadId];
                uint unread = thread.Unread;
                await Task.Run(() =>
                {
                    SignalDBContext.SaveMessageLocked(message);
                });
                MoveThreadToTop(thread);
                if (SelectedThread == thread)
                {
                    View.Thread.Append(message);
                    View.Thread.ScrollToBottom();
                    if (message.Type == SignalMessageType.Synced)
                    {
                        View.Thread.AddToCache(message);
                    }
                }
                else
                {
                    if (message.Type == SignalMessageType.Incoming)
                    {
                        unread++;
                        thread.Unread = unread;
                        thread.LastActiveTimestamp = message.ReceivedTimestamp;
                        ThreadsDictionary[message.ThreadId].View.UnreadCount = unread;
                        await Task.Run(() =>
                        {
                            SignalDBContext.UpdateConversationLocked(message.ThreadId, unread);
                        });
                    }
                }
            }
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