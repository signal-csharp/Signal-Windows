using GalaSoft.MvvmLight;
using libsignalservice.crypto;
using libsignalservice.messages;
using libsignalservice.push;
using libsignalservice.util;
using Microsoft.EntityFrameworkCore;
using Nito.AsyncEx;
using Signal_Windows.Models;
using Signal_Windows.Signal;
using Signal_Windows.Storage;
using Strilanc.Value;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
        public string SelectedThread;
        public Manager SignalManager = null;
        public volatile bool Running = true;
        private CancellationTokenSource CancelSource = new CancellationTokenSource();
        public AsyncManualResetEvent IncomingOffSwitch = new AsyncManualResetEvent(false);
        public AsyncManualResetEvent OutgoingOffSwitch = new AsyncManualResetEvent(false);
        public AsyncManualResetEvent DBOffSwitch = new AsyncManualResetEvent(false);

        #region Contacts

        private object ContactsLock = new object();
        public ObservableCollection<SignalContact> Contacts = new ObservableCollection<SignalContact>();
        private Dictionary<string, SignalContact> ContactsDictionary = new Dictionary<string, SignalContact>();

        public void AddContacts(IEnumerable<SignalContact> contacts)
        {
            lock (ContactsLock)
            {
                foreach (SignalContact contact in contacts)
                {
                    Contacts.Add(contact);
                    ContactsDictionary[contact.UserName] = contact;
                }
            }
        }

        public void AddContact(SignalContact contact)
        {
            lock (ContactsLock)
            {
                Contacts.Add(contact);
                ContactsDictionary[contact.UserName] = contact;
            }
        }

        public SignalContact GetOrCreateContact(string username)
        {
            lock (ContactsLock)
            {
                if (ContactsDictionary.ContainsKey(username))
                {
                    return ContactsDictionary[username];
                }
                else
                {
                    SignalContact contact = new SignalContact()
                    {
                        UserName = username,
                        ContactDisplayName = username,
                        Color = "#ff0000"
                    };
                    using (var ctx = new SignalDBContext())
                    {
                        ctx.Contacts.Add(contact);
                        ctx.SaveChanges();
                    }
                    Task.Run(async () =>
                    {
                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            AddContact(contact);
                        });
                    });
                    return contact;
                }
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
                    try
                    {
                        await Task.Run(async () =>
                        {
                            using (var ctx = new SignalDBContext())
                            {
                                var contacts = ctx.Contacts.AsNoTracking().ToList();
                                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                {
                                    AddContacts(contacts);
                                });
                            }
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
                    SignalContact contact = (SignalContact)e.AddedItems[0];
                    SelectedThread = contact.UserName;
                    Thread.ThreadTitle = contact.ContactDisplayName;
                    Thread.Messages.Clear();
                    var messages = await Task.Run(() =>
                    {
                        using (var ctx = new SignalDBContext())
                        {
                            return ctx.Messages
                                .Where(m => m.ThreadID == SelectedThread)
                                .Include(m => m.Author)
                                .Include(m => m.Attachments)
                                .AsNoTracking().ToList();
                        }
                    });
                    foreach (var m in messages)
                    {
                        Thread.Messages.Add(m);
                    }
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
                        ThreadID = SelectedThread,
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
                if (SelectedThread == message.ThreadID)
                {
                    Thread.Messages.Add(message);
                    View.ScrollToBottom();
                }
            }
        }

        public void UIHandleOutgoingMessage(SignalMessage message)
        {
            SignalMessage[] messages = new SignalMessage[] { message };
            DBQueue.Add(new Tuple<SignalMessage[], bool>(messages, false));
            Thread.Messages.Add(message);
            View.ScrollToBottom();
            OutgoingQueue.Add(message);
        }

        #endregion UIThread
    }
}