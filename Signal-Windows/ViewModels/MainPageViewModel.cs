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
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using static libsignalservice.SignalServiceMessagePipe;

namespace Signal_Windows.ViewModels
{
    public partial class MainPageViewModel : ViewModelBase, MessagePipeCallback
    {
        private ApplicationDataContainer LocalSettings = ApplicationData.Current.LocalSettings;
        private AsyncLock ActionInProgress = new AsyncLock();
        public ThreadViewModel Thread { get; set; }
        public MainPage View;
        public SignalThread SelectedThread;
        public volatile bool Running = true;
        private CancellationTokenSource CancelSource = new CancellationTokenSource();
        public AsyncManualResetEvent IncomingOffSwitch = new AsyncManualResetEvent(false);
        public AsyncManualResetEvent OutgoingOffSwitch = new AsyncManualResetEvent(false);

        private SignalServiceMessagePipe Pipe;
        private SignalServiceMessageSender MessageSender;
        private SignalServiceMessageReceiver MessageReceiver;

        #region Contacts

        public ObservableCollection<SignalThread> Threads = new ObservableCollection<SignalThread>();
        private Dictionary<string, SignalThread> ThreadsDictionary = new Dictionary<string, SignalThread>();

        public void AddThreads(IEnumerable<SignalThread> contacts)
        {
            foreach (SignalThread thread in contacts)
            {
                Threads.Add(thread);
                ThreadsDictionary[thread.ThreadId] = thread;
            }
        }

        public void AddThread(SignalThread contact)
        {
            Threads.Add(contact);
            ThreadsDictionary[contact.ThreadId] = contact;
        }

        public void UIUpdateThread(SignalThread thread)
        {
            SignalThread uiThread = ThreadsDictionary[thread.ThreadId];
            uiThread.ThreadDisplayName = thread.ThreadDisplayName;
            uiThread.LastActiveTimestamp = thread.LastActiveTimestamp;
            uiThread.Draft = thread.Draft;
            uiThread.Unread = thread.Unread;
            uiThread.AvatarFile = thread.AvatarFile;
            uiThread.View.Reload();
            if (SelectedThread != null && uiThread.ThreadId == SelectedThread.ThreadId)
            {
                Thread.Update(thread);
            }
        }

        #endregion Contacts

        public MainPageViewModel()
        {
            Thread = new ThreadViewModel(this);
            var l = ActionInProgress.Lock(CancelSource.Token);
            try
            {
                Task.Run(async () =>
                {
                    List<SignalContact> contacts = SignalDBContext.GetAllContactsLocked();
                    List<SignalGroup> groups = SignalDBContext.GetAllGroupsLocked();
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        AddThreads(groups);
                        AddThreads(contacts);
                    });
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        MessageReceiver = new SignalServiceMessageReceiver(CancelSource.Token, App.ServiceUrls, new StaticCredentialsProvider(App.Store.Username, App.Store.Password, App.Store.SignalingKey, (int)App.Store.DeviceId), App.USER_AGENT);
                        Pipe = MessageReceiver.createMessagePipe();
                        MessageSender = new SignalServiceMessageSender(CancelSource.Token, App.ServiceUrls, App.Store.Username, App.Store.Password, (int)App.Store.DeviceId, new Store(), Pipe, null, App.USER_AGENT);
                        Task.Factory.StartNew(HandleIncomingMessages, TaskCreationOptions.LongRunning);
                        Task.Factory.StartNew(HandleOutgoingMessages, TaskCreationOptions.LongRunning);
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

        internal async void ContactsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                using (await ActionInProgress.LockAsync())
                {
                    SelectedThread = (SignalThread)e.AddedItems[0];
                    await Thread.Load(SelectedThread);
                    View.ScrollToBottom();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }
        }

        internal async Task TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
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
                        Thread.Append(message);
                        View.ScrollToBottom();
                        await Task.Run(() =>
                        {
                            SignalDBContext.SaveMessageLocked(message);
                        });
                        if (SelectedThread != null && SelectedThread.ThreadId == message.ThreadId)
                        {
                            Thread.AddToCache(message);
                        }
                        OutgoingQueue.Add(message);
                        var after = Util.CurrentTimeMillis();
                        Debug.WriteLine("ms until out queue: " + (after - now));
                    }
                }
            }
        }

        internal void Cancel()
        {
            Running = false;
            CancelSource.Cancel();
        }

        #region UIThread

        public async Task UIHandleIncomingMessage(SignalMessage message)
        {
            using (await ActionInProgress.LockAsync())
            {
                await Task.Run(() =>
                {
                    SignalDBContext.SaveMessageLocked(message);
                });
                if (SelectedThread != null && SelectedThread.ThreadId == message.ThreadId)
                {
                    Thread.Append(message);
                    View.ScrollToBottom();
                    if (message.Type == SignalMessageType.Synced)
                    {
                        Thread.AddToCache(message);
                    }
                }
            }
        }

        public void UIHandleSuccessfullSend(SignalMessage updatedMessage)
        {
            if (SelectedThread != null && SelectedThread.ThreadId == updatedMessage.ThreadId)
            {
                Thread.UpdateMessageBox(updatedMessage);
            }
        }

        public void UIHandleReceiptReceived(SignalMessage updatedMessage)
        {
            if (SelectedThread != null && SelectedThread.ThreadId == updatedMessage.ThreadId)
            {
                Thread.UpdateMessageBox(updatedMessage);
            }
        }

        #endregion UIThread
    }
}