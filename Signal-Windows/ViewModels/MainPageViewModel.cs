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
    public class MainPageViewModel : ViewModelBase, MessagePipeCallback
    {
        private ApplicationDataContainer LocalSettings = ApplicationData.Current.LocalSettings;
        private bool ActionInProgress = false;
        public ObservableCollection<SignalMessage> Messages = new ObservableCollection<SignalMessage>();
        private string _ThreadTitle;

        public string ThreadTitle
        {
            get
            {
                return _ThreadTitle;
            }
            set
            {
                _ThreadTitle = value;
                RaisePropertyChanged("ThreadTitle");
            }
        }

        public MainPage View;
        public string SelectedThread;
        public Manager SignalManager = null;
        public volatile bool Running = true;
        private CancellationTokenSource CancelSource = new CancellationTokenSource();
        private AsyncManualResetEvent SendSwitch = new AsyncManualResetEvent(false);
        private AsyncManualResetEvent DBSwitch = new AsyncManualResetEvent(false);
        private AsyncManualResetEvent MessageSavePendingSwitch = new AsyncManualResetEvent(false);
        public AsyncManualResetEvent IncomingOffSwitch = new AsyncManualResetEvent(false);
        public AsyncManualResetEvent OutgoingOffSwitch = new AsyncManualResetEvent(false);
        public AsyncManualResetEvent DBOffSwitch = new AsyncManualResetEvent(false);
        public ConcurrentQueue<SignalMessage> OutgoingQueue = new ConcurrentQueue<SignalMessage>();
        private ConcurrentQueue<Tuple<SignalMessage[], bool>> DBQueue = new ConcurrentQueue<Tuple<SignalMessage[], bool>>();

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
                    ThreadTitle = contact.ContactDisplayName;
                    Messages.Clear();
                    var messages = await Task.Run(() =>
                    {
                        using (var ctx = new SignalDBContext())
                        {
                            return ctx.Messages
                                .Where(m => m.ThreadID == SelectedThread)
                                .Include(m => m.Author)
                                .AsNoTracking().ToList();
                        }
                    });
                    foreach (var m in messages)
                    {
                        Messages.Add(m);
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
                    SignalMessage sm = new SignalMessage()
                    {
                        Author = null,
                        ComposedTimestamp = Util.CurrentTimeMillis(),
                        Content = t.Text,
                        ThreadID = SelectedThread,
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

        #region MessagesDB

        private void HandleDBQueue()
        {
            Debug.WriteLine("HandleDBQueue starting...");
            try
            {
                while (Running)
                {
                    DBSwitch.Wait(CancelSource.Token);
                    DBSwitch.Reset();
                    Tuple<SignalMessage[], bool> t;
                    if (DBQueue.TryDequeue(out t))
                    {
                        using (var ctx = new SignalDBContext())
                        {
                            foreach (var message in t.Item1)
                            {
                                SignalContact author = ctx.Contacts.SingleOrDefault(b => b.UserName == message.AuthorUsername);
                                if (author == null && t.Item2)
                                { //TODO lock, display
                                    author = new SignalContact()
                                    {
                                        UserName = message.AuthorUsername,
                                        ContactDisplayName = message.AuthorUsername
                                    };
                                    ctx.Contacts.Add(author);
                                    ctx.SaveChanges();
                                }
                                message.Author = author;
                                ctx.Messages.Add(message);
                                if (message.Type == (uint)SignalMessageType.Incoming || message.DeviceId != (int)LocalSettings.Values["DeviceId"])
                                {
                                    if (message.Attachments != null && message.Attachments.Count > 0)
                                    {
                                        ctx.SaveChanges();
                                        HandleDBAttachments(message, ctx);
                                    }
                                }
                            }
                            ctx.SaveChanges();
                            if (t.Item2)
                            {
                                MessageSavePendingSwitch.Set();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);
            }
            DBOffSwitch.Set();
            Debug.WriteLine("HandleDBQueue finished");
        }

        private void HandleDBAttachments(SignalMessage message, SignalDBContext ctx)
        {
            int i = 0;
            foreach (var sa in message.Attachments)
            {
                sa.FileName = "attachment_" + message.Id + "_" + i;
                Task.Run(() =>
                {
                    try
                    {
                        DirectoryInfo di = Directory.CreateDirectory(Manager.localFolder + @"\Attachments");
                        using (var cipher = File.Open(Manager.localFolder + @"\Attachments\" + sa.FileName + ".cipher", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                        using (var plain = File.OpenWrite(Manager.localFolder + @"\Attachments\" + sa.FileName))
                        {
                            SignalManager.MessageReceiver.retrieveAttachment(new SignalServiceAttachmentPointer(sa.StorageId, sa.ContentType, sa.Key, sa.Relay), plain, cipher);
                            //TODO notify UI
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                        Debug.WriteLine(e.StackTrace);
                    }
                });
                i++;
            }
        }

        #endregion MessagesDB

        #region UIThread

        public void UIHandleIncomingMessages(SignalMessage[] messages)
        {
            DBQueue.Enqueue(new Tuple<SignalMessage[], bool>(messages, true));
            DBSwitch.Set();
            foreach (var message in messages)
            {
                if (SelectedThread == message.ThreadID)
                {
                    Messages.Add(message);
                    View.ScrollToBottom();
                }
            }
        }

        public void UIHandleOutgoingMessage(SignalMessage message)
        {
            SignalMessage[] messages = new SignalMessage[] { message };
            DBQueue.Enqueue(new Tuple<SignalMessage[], bool>(messages, false));
            DBSwitch.Set();
            Messages.Add(message);
            View.ScrollToBottom();
            OutgoingQueue.Enqueue(message);
            SendSwitch.Set();
        }

        #endregion UIThread

        #region Sender

        public void HandleOutgoingMessages()
        {
            Debug.WriteLine("HandleOutgoingMessages starting...");
            try
            {
                while (Running)
                {
                    SendSwitch.Wait(CancelSource.Token);
                    SendSwitch.Reset();
                    SignalMessage t;
                    while (OutgoingQueue.TryDequeue(out t))
                    {
                        Builder messageBuilder = SignalServiceDataMessage.newBuilder().withBody(t.Content).withTimestamp(t.ComposedTimestamp);
                        List<SignalServiceAddress> recipients = new List<SignalServiceAddress>();
                        if (t.ThreadID[0] == '+')
                        {
                            recipients.Add(new SignalServiceAddress(t.ThreadID));
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        SignalServiceDataMessage ssdm = messageBuilder.build();
                        SignalManager.sendMessage(recipients, ssdm);
                    }
                }
            }
            catch (Exception) { }
            Debug.WriteLine("HandleOutgoingMessages finished");
            OutgoingOffSwitch.Set();
        }

        #endregion Sender

        #region Receiver

        public void HandleIncomingMessages()
        {
            Debug.WriteLine("HandleIncomingMessages starting...");
            try
            {
                while (Running)
                {
                    SignalManager.ReceiveBatch(this);
                }
            }
            catch (Exception) { }
            IncomingOffSwitch.Set();
            Debug.WriteLine("HandleIncomingMessages finished");
        }

        public void onMessages(SignalServiceEnvelope[] envelopes)
        {
            List<SignalMessage> messages = new List<SignalMessage>();
            foreach (var envelope in envelopes)
            {
                try
                {
                    var cipher = new SignalServiceCipher(new SignalServiceAddress((string)LocalSettings.Values["Username"]), SignalManager.SignalStore);
                    var content = cipher.decrypt(envelope);

                    //TODO handle special messages & unknown groups
                    if (content.getDataMessage().HasValue)
                    {
                        SignalServiceDataMessage message = content.getDataMessage().ForceGetValue();
                        if (message.isEndSession())
                        {
                            SignalManager.SignalStore.DeleteAllSessions(envelope.getSource());
                            SignalManager.Save();
                        }
                        else
                        {
                            messages.Add(HandleMessage(envelope, content, message));
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine(e.StackTrace);
                }
                finally
                {
                    SignalManager.Save();
                }
            }
            if (messages.Count > 0)
            {
                MessageSavePendingSwitch.Reset();
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    UIHandleIncomingMessages(messages.ToArray());
                }).AsTask().Wait();
                MessageSavePendingSwitch.Wait(CancelSource.Token);
            }
        }

        private SignalMessage HandleMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage)
        {
            string source = envelope.getSource();
            SignalContact author = GetOrCreateContact(source);
            string body = dataMessage.getBody().HasValue ? dataMessage.getBody().ForceGetValue() : "";
            string threadId = dataMessage.getGroupInfo().HasValue ? Base64.encodeBytes(dataMessage.getGroupInfo().ForceGetValue().getGroupId()) : source;
            List<SignalAttachment> attachments = new List<SignalAttachment>();
            SignalMessage message = new SignalMessage()
            {
                Type = source == (string)LocalSettings.Values["Username"] ? (uint)SignalMessageType.Outgoing : (uint)SignalMessageType.Incoming,
                Status = (uint)SignalMessageStatus.Default,
                Author = author,
                Content = body,
                ThreadID = source,
                AuthorUsername = source,
                DeviceId = (uint)envelope.getSourceDevice(),
                Receipts = 0,
                ComposedTimestamp = envelope.getTimestamp(),
                ReceivedTimestamp = Util.CurrentTimeMillis(),
                AttachmentsCount = (uint)attachments.Count,
                Attachments = attachments
            };
            if (dataMessage.getAttachments().HasValue)
            {
                var receivedAttachments = dataMessage.getAttachments().ForceGetValue();
                foreach (var receivedAttachment in receivedAttachments)
                {
                    var pointer = receivedAttachment.asPointer();
                    SignalAttachment sa = new SignalAttachment()
                    {
                        Message = message,
                        Status = (uint)SignalAttachmentStatus.Default,
                        ContentType = "",
                        Key = pointer.getKey(),
                        Relay = pointer.getRelay(),
                        StorageId = pointer.getId()
                    };
                    attachments.Add(sa);
                }
            }
            Debug.WriteLine("received message: " + message.Content);
            return message;
        }

        #endregion Receiver
    }
}