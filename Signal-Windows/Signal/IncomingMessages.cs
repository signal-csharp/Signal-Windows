using libsignalservice.crypto;
using libsignalservice.messages;
using libsignalservice.push;
using libsignalservice.util;
using Nito.AsyncEx;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Strilanc.Value;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Signal_Windows.ViewModels
{
    public partial class MainPageViewModel
    {
        /// <summary>
        /// ResetEvent that indicates the end of the pending db transactions
        /// </summary>
        private AsyncManualResetEvent IncomingMessageSavedEvent = new AsyncManualResetEvent(false);

        /// <summary>
        /// Reads, decrypts, handles and schedules storing and displaying of incoming messages from the pipe
        /// </summary>
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

        /// <summary>
        /// onMessages is called from the pipe after it received messages
        /// </summary>
        /// <param name="envelopes"></param>
        public void onMessages(SignalServiceEnvelope[] envelopes)
        {
            List<SignalMessage> messages = new List<SignalMessage>();
            foreach (var envelope in envelopes)
            {
                if (envelope == null)
                {
                    continue;
                }
                try
                {
                    if (envelope.isReceipt())
                    {
                        HandleReceipt(envelope);
                    }
                    else if (envelope.isPreKeySignalMessage() || envelope.isSignalMessage())
                    {
                        SignalMessage sm = HandleMessage(envelope);
                        if (sm != null)
                        {
                            messages.Add(sm);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("received message of unknown type " + envelope.getType() + " from " + envelope.getSource());
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine(e.StackTrace);
                }
            }
            if (messages.Count > 0)
            {
                IncomingMessageSavedEvent.Reset();
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    UIHandleIncomingMessages(messages.ToArray());
                }).AsTask().Wait();
                IncomingMessageSavedEvent.Wait(CancelSource.Token);
            }
        }

        private void HandleReceipt(SignalServiceEnvelope envelope)
        {
            lock (SignalDBContext.DBLock)
            {
                using (var ctx = new SignalDBContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction())
                    {
                        var m = ctx.Messages.
                            SingleOrDefault(t => t.ComposedTimestamp == envelope.getTimestamp() && t.Author == null);
                        if (m != null)
                        {
                            m.Receipts++;
                            ctx.SaveChanges();
                            transaction.Commit();
                            if (m.Receipts > 0 && m.Status == (uint)SignalMessageStatus.Confirmed)
                            {
                                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                {
                                    UIHandleReceiptReceived(m);
                                }).AsTask().Wait();
                            }
                        }
                        else
                        {
                            Debug.WriteLine("HandleReceipt could not find the correspoding message");
                        }
                    }
                }
            }
        }

        private SignalMessage HandleMessage(SignalServiceEnvelope envelope)
        {
            var cipher = new SignalServiceCipher(new SignalServiceAddress((string)LocalSettings.Values["Username"]), SignalManager.SignalStore);
            var content = cipher.decrypt(envelope);

            if (content.getDataMessage().HasValue)
            {
                SignalServiceDataMessage message = content.getDataMessage().ForceGetValue();
                if (message.isEndSession())
                {
                    SignalManager.SignalStore.DeleteAllSessions(envelope.getSource());
                }
                else if (message.isGroupUpdate())
                {
                    HandleGroupUpdateMessage(envelope, content, message);
                }
                else if (message.isExpirationUpdate())
                {
                    //TODO
                }
                else
                {
                    //TODO check both the db and the previous messages for duplicates
                    return HandleSignalMessage(envelope, content, message);
                }
            }
            else if (content.getSyncMessage().HasValue)
            {
                //TODO
            } //TODO callmessages
            else
            {
                Debug.WriteLine("HandleMessage got unrecognized message from " + envelope.getSource());
            }
            return null;
        }

        private void HandleGroupUpdateMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage)
        {
            if (dataMessage.getGroupInfo().HasValue) //check signal droid
            {
                SignalServiceGroup group = dataMessage.getGroupInfo().ForceGetValue();
                lock (SignalDBContext.DBLock)
                    using (var ctx = new SignalDBContext())
                    {
                        var dbgroup = GetOrCreateGroup(ctx, Base64.encodeBytes(group.getGroupId()));
                        if (group.getName().HasValue)
                        {
                            dbgroup.ThreadDisplayName = group.getName().ForceGetValue();
                        }
                        if (group.getMembers().HasValue)
                        {
                            foreach (var member in group.getMembers().ForceGetValue())
                            {
                                bool n = true;
                                foreach (var m in dbgroup.GroupMemberships)
                                {
                                    if (m.Contact.ThreadId == member)
                                    {
                                        n = false;
                                    }
                                }
                                if (n && member != (string)LocalSettings.Values["Username"])
                                {
                                    dbgroup.GroupMemberships.Add(new GroupMembership()
                                    {
                                        Contact = GetOrCreateContact(ctx, member),
                                        Group = dbgroup
                                    });
                                }
                            }
                        }
                        ctx.SaveChanges();
                    }
            }
            else
            {
                Debug.WriteLine("received group update without group info!");
            }
        }

        private SignalMessage HandleSignalMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage)
        {
            string source = envelope.getSource();
            string thread = dataMessage.getGroupInfo().HasValue ? Base64.encodeBytes(dataMessage.getGroupInfo().ForceGetValue().getGroupId()) : source;
            SignalContact author = GetOrCreateContactLocked(source);
            string body = dataMessage.getBody().HasValue ? dataMessage.getBody().ForceGetValue() : "";
            string threadId = dataMessage.getGroupInfo().HasValue ? Base64.encodeBytes(dataMessage.getGroupInfo().ForceGetValue().getGroupId()) : source;
            List<SignalAttachment> attachments = new List<SignalAttachment>();
            SignalMessage message = new SignalMessage()
            {
                Type = source == (string)LocalSettings.Values["Username"] ? (uint)SignalMessageType.Outgoing : (uint)SignalMessageType.Incoming,
                Status = (uint)SignalMessageStatus.Pending,
                Author = author,
                Content = body,
                ThreadID = thread,
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
    }
}