using GalaSoft.MvvmLight;
using libsignalservice.util;
using Nito.AsyncEx;
using Signal_Windows.Models;
using Signal_Windows.Signal;
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
        private bool ActionInProgress = true;
        public ThreadViewModel Thread { get; set; }
        public MainPage View;
        public SignalThread SelectedThread;
        public Manager SignalManager = null;
        public volatile bool Running = true;
        private CancellationTokenSource CancelSource = new CancellationTokenSource();
        public AsyncManualResetEvent IncomingOffSwitch = new AsyncManualResetEvent(false);
        public AsyncManualResetEvent OutgoingOffSwitch = new AsyncManualResetEvent(false);
        public AsyncManualResetEvent DBOffSwitch = new AsyncManualResetEvent(false);

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
            uiThread.LastMessage = thread.LastMessage;
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
                    var manager = new Manager(CancelSource.Token, (string)LocalSettings.Values["Username"], true);
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        SignalManager = manager;
                        Task.Factory.StartNew(HandleIncomingMessages, TaskCreationOptions.LongRunning);
                        Task.Factory.StartNew(HandleOutgoingMessages, TaskCreationOptions.LongRunning);
                        Task.Factory.StartNew(HandleDBQueue, TaskCreationOptions.LongRunning);
                        ActionInProgress = false;
                    });
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
                if (!ActionInProgress)
                {
                    ActionInProgress = true;
                    SelectedThread = (SignalThread)e.AddedItems[0];
                    await Thread.Load(SelectedThread);
                    ActionInProgress = false;
                    View.ScrollToBottom();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }
        }

        internal void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                TextBox t = (TextBox)sender;
                if (t.Text != "")
                {
                    var now = Util.CurrentTimeMillis();
                    SignalMessage sm = new SignalMessage()
                    {
                        Author = null,
                        ComposedTimestamp = now,
                        Content = new SignalMessageContent() { Content = t.Text },
                        ThreadID = SelectedThread.ThreadId,
                        ReceivedTimestamp = now,
                        Type = 0
                    };
                    UIHandleOutgoingMessage(sm);
                    t.Text = "";
                }
            }
        }

        internal void Cancel()
        {
            Running = false;
            CancelSource.Cancel();
        }

        #region UIThread

        public void UIHandleIncomingMessages(SignalMessage[] messages)
        {
            DBQueue.Add(new Tuple<SignalMessage[], bool>(messages, true));
            foreach (var message in messages)
            {
                if (SelectedThread != null && SelectedThread.ThreadId == message.ThreadID)
                {
                    Thread.Append(message);
                    View.ScrollToBottom();
                }
            }
        }

        public void UIHandleOutgoingMessage(SignalMessage message)
        {
            SignalMessage[] messages = new SignalMessage[] { message };
            Thread.Append(message);
            View.ScrollToBottom();
            DBQueue.Add(new Tuple<SignalMessage[], bool>(messages, false));
        }

        public void UIHandleOutgoingSaved(SignalMessage originalMessage)
        {
            if (SelectedThread != null && SelectedThread.ThreadId == originalMessage.ThreadID)
            {
                Thread.AddToCache(originalMessage);
            }
        }

        public void UIHandleSuccessfullSend(SignalMessage updatedMessage)
        {
            if (SelectedThread != null && SelectedThread.ThreadId == updatedMessage.ThreadID)
            {
                Thread.UpdateMessageBox(updatedMessage);
            }
        }

        public void UIHandleReceiptReceived(SignalMessage updatedMessage)
        {
            if (SelectedThread != null && SelectedThread.ThreadId == updatedMessage.ThreadID)
            {
                Thread.UpdateMessageBox(updatedMessage);
            }
        }

        #endregion UIThread
    }
}
