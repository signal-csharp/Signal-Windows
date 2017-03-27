using GalaSoft.MvvmLight;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Nito.AsyncEx;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Signal_Windows.Signal;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using libsignalservice.messages;
using System.Collections.Concurrent;
using libsignalservice.push;
using Strilanc.Value;
using libsignalservice.crypto;
using libsignalservice.util;
using System.ComponentModel;
using static libsignalservice.SignalServiceMessagePipe;
using Microsoft.EntityFrameworkCore;
using Windows.UI.Xaml.Navigation;
using Windows.Storage;
using System.IO;

namespace Signal_Windows.ViewModels
{
    public class MainPageViewModel : ViewModelBase, MessagePipeCallback
    {
        ApplicationDataContainer LocalSettings = ApplicationData.Current.LocalSettings;
        public ObservableCollection<SignalContact> Contacts = new ObservableCollection<SignalContact>();
        public ObservableCollection<SignalMessage> Messages = new ObservableCollection<SignalMessage>();
        public MainPage View;
        public string SelectedThread;
        public Manager SignalManager = null;
        public volatile bool Running = true;
        CancellationTokenSource CancelSource = new CancellationTokenSource();
        private AsyncManualResetEvent SendSwitch = new AsyncManualResetEvent(false);
        private AsyncManualResetEvent DBSwitch = new AsyncManualResetEvent(false);
        private AsyncManualResetEvent MessageSavePendingSwitch = new AsyncManualResetEvent(false);
        public AsyncManualResetEvent IncomingOffSwitch = new AsyncManualResetEvent(false);
        public AsyncManualResetEvent OutgoingOffSwitch = new AsyncManualResetEvent(false);
        public AsyncManualResetEvent DBOffSwitch = new AsyncManualResetEvent(false);
        public ConcurrentQueue<SignalMessage> OutgoingQueue = new ConcurrentQueue<SignalMessage>();
        private ConcurrentQueue<Tuple<SignalMessage[], bool>> DBQueue= new ConcurrentQueue<Tuple<SignalMessage[], bool>>();

        public MainPageViewModel()
        {
            try
            {
                Task.Run(async () =>
                {
                    try
                    {
                        Task.Run(() =>
                        {
                            using (var ctx = new SignalDBContext())
                            {
                                List<SignalContact> loadedContacts = new List<SignalContact>();
                                var contacts = ctx.Contacts.AsNoTracking().ToList();
                                //http://stackoverflow.com/questions/670577/observablecollection-doesnt-support-addrange-method-so-i-get-notified-for-each
                                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                {
                                    foreach (var contact in contacts)
                                    {
                                        Contacts.Add(contact);
                                    }
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
                    catch(Exception e)
                    {
                        Debug.WriteLine("Setup task crashed.");
                        Debug.WriteLine(e.Message);
                        Debug.WriteLine(e.StackTrace);
                    }
                });
            }
            catch(Exception e)
            {
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);
            }
        }

        internal void ContactsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SignalContact contact = (SignalContact) e.AddedItems[0];
            SelectedThread = contact.UserName;
            Messages.Clear();
            using (var ctx = new SignalDBContext())
            {
                var messages = ctx.Messages.Where(b => b.ThreadID == SelectedThread).AsNoTracking();
                foreach (var message in messages)
                {
                    Messages.Add(message);
                }
                View.ScrollToBottom();
            }
        }

        internal void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if(e.Key == VirtualKey.Enter)
            {
                TextBox t = (TextBox)sender;
                if(t.Text != "")
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
                                if (message.Type == (uint)SignalMessageType.Incoming)
                                {
                                    if (message.AttachmentList != null && message.AttachmentList.Count > 0)
                                    {
                                        ctx.SaveChanges();
                                        HandleDBAttachments(message, message.AttachmentList);
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
            catch (Exception e) {
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);
            }
            DBOffSwitch.Set();
            Debug.WriteLine("HandleDBQueue finished");
        }

        private void HandleDBAttachments(SignalMessage message, List<SignalServiceAttachment> list)
        {
            int i = 0;
            foreach (var attachment in list)
            {
                var pointer = attachment.asPointer();
                SignalAttachment sa = new SignalAttachment()
                {
                    FileName = "attachment_" + message.Id + "_" + i,
                    Message = message,
                    Status = (uint)SignalAttachmentStatus.Default,
                    ContentType = "",
                    Key = pointer.getKey(),
                    Relay = pointer.getRelay(),
                    StorageId = pointer.getId()
                };
                Task.Run(() =>
                {
                    try
                    {
                        DirectoryInfo di = Directory.CreateDirectory(Manager.localFolder + @"\Attachments");
                        using (var cipher = File.Open(Manager.localFolder + @"\Attachments\" + sa.FileName + ".cipher", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                        using (var plain = File.OpenWrite(Manager.localFolder + @"\Attachments\" + sa.FileName))
                        {
                            SignalManager.MessageReceiver.retrieveAttachment(pointer, plain, cipher);
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

        #endregion

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
        #endregion

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
        #endregion

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
                if (envelope.isSignalMessage())
                {
                    try
                    {
                        var cipher = new SignalServiceCipher(new SignalServiceAddress((string) LocalSettings.Values["Username"]), SignalManager.SignalStore);
                        var content = cipher.decrypt(envelope);

                        //TODO handle special messages & unknown groups
                        if (content.getDataMessage().HasValue)
                        {
                            SignalServiceDataMessage message = content.getDataMessage().ForceGetValue();
                            messages.Add(HandleMessage(envelope, content, message));
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
            }
            MessageSavePendingSwitch.Reset();
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                UIHandleIncomingMessages(messages.ToArray());
            }).AsTask().Wait();
            MessageSavePendingSwitch.Wait(CancelSource.Token);
        }

        private SignalMessage HandleMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage)
        {
            string source = envelope.getSource();
            string body = dataMessage.getBody().HasValue ? dataMessage.getBody().ForceGetValue() : "";
            string threadId = dataMessage.getGroupInfo().HasValue ? Base64.encodeBytes(dataMessage.getGroupInfo().ForceGetValue().getGroupId()) : source;
            List<SignalServiceAttachment> attachments = new List<SignalServiceAttachment>();
            if(dataMessage.getAttachments().HasValue)
            {
                attachments = dataMessage.getAttachments().ForceGetValue();
            }
            SignalMessage message = new SignalMessage()
            {
                Type = source == (string)LocalSettings.Values["Username"] ? (uint)SignalMessageType.Outgoing : (uint)SignalMessageType.Incoming,
                Status = (uint) SignalMessageStatus.Default,
                Content = body,
                ThreadID = source,
                AuthorUsername = source,
                DeviceId = (uint) envelope.getSourceDevice(),
                Receipts = 0,
                ComposedTimestamp = envelope.getTimestamp(),
                ReceivedTimestamp = Util.CurrentTimeMillis(),
                Attachments = (uint) attachments.Count,
                AttachmentList = attachments
            };
            Debug.WriteLine("received message: "+message.Content);
            return message;
        }
        #endregion
    }
}
