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

namespace Signal_Windows.ViewModels
{
    public class MainPageViewModel : ViewModelBase, MessagePipeCallback
    {
        ApplicationDataContainer LocalSettings = ApplicationData.Current.LocalSettings;
        public ObservableCollection<SignalContact> Contacts = new ObservableCollection<SignalContact>();
        public ObservableCollection<SignalMessage> Messages = new ObservableCollection<SignalMessage>();
        public MainPage View;
        public SignalContact SelectedThread;
        public Manager SignalManager = null;
        public volatile bool Running = true;
        CancellationTokenSource CancelSource = new CancellationTokenSource();
        private AsyncManualResetEvent SendSwitch = new AsyncManualResetEvent(false);
        public AsyncManualResetEvent IncomingOffSwitch = new AsyncManualResetEvent(false);
        public AsyncManualResetEvent OutgoingOffSwitch = new AsyncManualResetEvent(false);
        public ConcurrentQueue<SignalMessage> OutgoingQueue = new ConcurrentQueue<SignalMessage>();

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
            SelectedThread = contact;
            Messages.Clear();
            using (var ctx = new SignalDBContext())
            {
                var messages = ctx.Messages.Where(b => b.ThreadID == SelectedThread.UserName).AsNoTracking();
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
                        ThreadID = SelectedThread.UserName,
                        Type = 0
                    };
                    Messages.Add(sm);
                    View.ScrollToBottom();
                    OutgoingQueue.Enqueue(sm);
                    SendSwitch.Set();
                    t.Text = "";
                }
            }
        }

        internal void Cancel()
        {
            Running = false;
            CancelSource.Cancel();
        }

        #region Background

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
                        using (var ctx = new SignalDBContext())
                        {
                            ctx.Messages.Add(t);
                            ctx.SaveChanges();
                        }
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

        public void onMessages(SignalServiceEnvelope[] envelopes)
        {
            using (var ctx = new SignalDBContext())
            {
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
                                handleTextMessage(ctx, envelope, content, message);
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
                ctx.SaveChanges();
            }
        }

        private void handleTextMessage(SignalDBContext ctx, SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage)
        {
            string source = envelope.getSource();
            SignalContact author = ctx.Contacts.SingleOrDefault(b => b.UserName == source);
            if(author == null)
            {
                author = new SignalContact()
                {
                    UserName = source,
                    ContactDisplayName = source
                };
                ctx.Contacts.Add(author);
            }
            string body = dataMessage.getBody().HasValue ? dataMessage.getBody().ForceGetValue() : "";
            string threadId = dataMessage.getGroupInfo().HasValue ? Base64.encodeBytes(dataMessage.getGroupInfo().ForceGetValue().getGroupId()) : source;
            SignalMessage message = new SignalMessage()
            {
                Author = author,
                Content = body,
                ComposedTimestamp = envelope.getTimestamp(),
                ReceivedTimestamp = 0,
                ThreadID = source,
                Type = source == (string)LocalSettings.Values["Username"] ? 0u : 1u
            };
            ctx.Messages.Add(message);
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (SelectedThread.UserName == source)
                {
                    Messages.Add(message);
                    View.ScrollToBottom();
                    }
            }).AsTask().Wait();
            Debug.WriteLine(message.Content);
        }
#endregion
    }
}
