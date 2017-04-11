using GalaSoft.MvvmLight;
using libsignalservice.util;
using Microsoft.EntityFrameworkCore;
using Nito.AsyncEx;
using Signal_Windows.Models;
using Signal_Windows.Signal;
using Signal_Windows.Storage;
using Strilanc.Value;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
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
        private bool ActionInProgress = false;
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

        public SignalContact GetOrCreateContact(SignalDBContext ctx, string username)
        {
            SignalContact contact = ctx.Contacts
                .Where(c => c.ThreadId == username)
                .SingleOrDefault();
            if (contact == null)
            {
                contact = new SignalContact()
                {
                    ThreadId = username,
                    ThreadDisplayName = username,
                    //TODO pick random color
                };
                ctx.Contacts.Add(contact);
                Task.Run(async () =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        AddThread(contact);
                    });
                }).Wait();
            }
            return contact;
        }

        public SignalContact GetOrCreateContactLocked(string username)
        {
            lock (SignalDBContext.DBLock)
                using (var ctx = new SignalDBContext())
                {
                    var t = GetOrCreateContact(ctx, username);
                    ctx.SaveChanges();
                    return t;
                }
        }

        public SignalGroup GetOrCreateGroup(SignalDBContext ctx, string threadid)
        {
            SignalGroup dbgroup = ctx.Groups
                .Where(g => g.ThreadId == threadid)
                .Include(g => g.GroupMemberships)
                .ThenInclude(gm => gm.Contact)
                .SingleOrDefault();
            if (dbgroup == null)
            {
                dbgroup = new SignalGroup()
                {
                    ThreadId = threadid,
                    ThreadDisplayName = "Unknown",
                    LastActiveTimestamp = Util.CurrentTimeMillis(),
                    AvatarFile = null,
                    Unread = 1,
                    GroupMemberships = new List<GroupMembership>()
                };
                ctx.Add(dbgroup);
                Task.Run(async () =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        AddThread(dbgroup);
                    });
                }).Wait();
            }
            return dbgroup;
        }

        public void UIUpdateThread(SignalThread thread)
        {
            //TODO
        }

        #endregion Contacts

        public MainPageViewModel()
        {
            Thread = new ThreadViewModel(this);
            try
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Run(async () =>
                        {
                            List<SignalContact> contacts = new List<SignalContact>();
                            List<SignalGroup> groups = new List<SignalGroup>();
                            lock (SignalDBContext.DBLock)
                            {
                                using (var ctx = new SignalDBContext())
                                {
                                    contacts = ctx.Contacts
                                    .AsNoTracking()
                                    .ToList();

                                    groups = ctx.Groups
                                    .Include(g => g.GroupMemberships)
                                    .ThenInclude(gm => gm.Contact)
                                    .AsNoTracking()
                                    .ToList();
                                }
                            }
                            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                AddThreads(groups);
                                AddThreads(contacts);
                            });
                        });
                        var manager = new Manager(CancelSource.Token, (string)LocalSettings.Values["Username"], true);
                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            SignalManager = manager;
                        });
                        Task.Factory.StartNew(HandleIncomingMessages, TaskCreationOptions.LongRunning);
                        Task.Factory.StartNew(HandleOutgoingMessages, TaskCreationOptions.LongRunning);
                        Task.Factory.StartNew(HandleDBQueue, TaskCreationOptions.LongRunning);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Setup task crashed.");
                        Debug.WriteLine(e.Message);
                        Debug.WriteLine(e.StackTrace);
                    }
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
                        Content = t.Text,
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

        private void UIHandleSuccessfullSend(SignalMessage updatedMessage)
        {
            if (SelectedThread != null && SelectedThread.ThreadId == updatedMessage.ThreadID)
            {
                Thread.UpdateMessageBox(updatedMessage);
            }
        }

        private void UIHandleReceiptReceived(SignalMessage updatedMessage)
        {
            if (SelectedThread != null && SelectedThread.ThreadId == updatedMessage.ThreadID)
            {
                Thread.UpdateMessageBox(updatedMessage);
            }
        }

        #endregion UIThread
    }
}