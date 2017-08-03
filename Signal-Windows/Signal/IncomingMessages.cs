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

namespace Signal_Windows.ViewModels
{
    public partial class MainPageViewModel
    {
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
                    try
                    {
                        Pipe.ReadBlocking(this);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                        Debug.WriteLine(e.StackTrace);
                    }
                }
            }
            catch (Exception) { }
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
                        SignalDBContext.IncreaseReceiptCountLocked(envelope, this);
                    }
                    else if (envelope.isPreKeySignalMessage() || envelope.isSignalMessage())
                    {
                        HandleMessage(envelope);
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
        }

        private void HandleMessage(SignalServiceEnvelope envelope)
        {
            try
            {
                var cipher = new SignalServiceCipher(new SignalServiceAddress(App.Store.Username), new Store());
                var content = cipher.decrypt(envelope);
                long timestamp = Util.CurrentTimeMillis();

                if (content.Message != null)
                {
                    SignalServiceDataMessage message = content.Message;
                    if (message.isEndSession())
                    {
                        SignalDBContext.DeleteAllSessions(envelope.getSource());
                    }
                    else if (message.isGroupUpdate())
                    {
                        HandleGroupUpdateMessage(envelope, content, message, timestamp);
                    }
                    else if (message.isExpirationUpdate())
                    {
                        string threadid;
                        if (message.getGroupInfo().HasValue)
                        {
                            threadid = Base64.encodeBytes(message.getGroupInfo().ForceGetValue().getGroupId());
                            SignalDBContext.GetOrCreateGroupLocked(threadid, timestamp, this);
                        }
                        else
                        {
                            threadid = envelope.getSource();
                            SignalDBContext.GetOrCreateContactLocked(threadid, timestamp, this);
                        }
                        SignalDBContext.UpdateExpiresInLocked(new SignalThread() { ThreadId = threadid }, (uint)message.getExpiresInSeconds());
                    }
                    else
                    {
                        //TODO check both the db for duplicates
                        HandleSignalMessage(envelope, content, message, false, timestamp);
                    }
                }
                else if (content.SynchronizeMessage != null)
                {
                    if (content.SynchronizeMessage.getSent().HasValue)
                    {
                        var syncMessage = content.SynchronizeMessage.getSent().ForceGetValue();
                        var dataMessage = syncMessage.getMessage();
                        //TODO check both the db for duplicates
                        if (dataMessage.isGroupUpdate())
                        {
                            HandleGroupUpdateMessage(envelope, content, dataMessage, timestamp);
                        }
                        else
                        {
                            HandleSignalMessage(envelope, content, dataMessage, true, timestamp);
                        }
                    }
                } //TODO callmessages
                else
                {
                    Debug.WriteLine("HandleMessage got unrecognized message from " + envelope.getSource());
                }
            }
            catch (libsignal.exceptions.UntrustedIdentityException e)
            {
                Debug.WriteLine("HandleMessage received message from changed identity");
                SignalDBContext.UpdateIdentityLocked(e.getName(), Base64.encodeBytes(e.getUntrustedIdentity().serialize()), VerifiedStatus.Default, this);
                HandleMessage(envelope);
            }
        }

        private void HandleGroupUpdateMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage, long timestamp)
        {
            if (dataMessage.getGroupInfo().HasValue) //TODO check signal droid: group messages have different types!
            {
                SignalServiceGroup group = dataMessage.getGroupInfo().ForceGetValue();
                SignalGroup g = new SignalGroup();
                string displayname = "Unknown group";
                string avatarfile = null;
                if (group.getName().HasValue)
                {
                    displayname = group.getName().ForceGetValue();
                }
                var dbgroup = SignalDBContext.InsertOrUpdateGroupLocked(Base64.encodeBytes(group.getGroupId()), displayname, avatarfile, true, timestamp, this);
                if (group.getMembers().HasValue)
                {
                    foreach (var member in group.getMembers().ForceGetValue())
                    {
                        SignalDBContext.InsertOrUpdateGroupMembershipLocked(dbgroup.Id, SignalDBContext.GetOrCreateContactLocked(member, timestamp, this).Id);
                    }
                }
            }
            else
            {
                Debug.WriteLine("received group update without group info!");
            }
        }

        private void HandleSignalMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage, bool isSync, long timestamp)
        {
            SignalMessageType type;
            SignalContact author;
            SignalMessageStatus status;
            string threadId;
            long composedTimestamp;
            string body = dataMessage.getBody().HasValue ? dataMessage.getBody().ForceGetValue() : "";

            if (dataMessage.getGroupInfo().HasValue)
            {
                threadId = Base64.encodeBytes(dataMessage.getGroupInfo().ForceGetValue().getGroupId());
                SignalDBContext.GetOrCreateGroupLocked(threadId, timestamp, this);
                composedTimestamp = envelope.getTimestamp();
            }
            else
            {
                if (isSync)
                {
                    var sent = content.SynchronizeMessage.getSent().ForceGetValue();
                    threadId = SignalDBContext.GetOrCreateContactLocked(sent.getDestination().ForceGetValue(), timestamp, this).ThreadId;
                    composedTimestamp = sent.getTimestamp();
                }
                else
                {
                    threadId = SignalDBContext.GetOrCreateContactLocked(envelope.getSource(), timestamp, this).ThreadId;
                    composedTimestamp = envelope.getTimestamp();
                }
            }

            if (isSync)
            {
                type = SignalMessageType.Synced;
                status = SignalMessageStatus.Confirmed;
                author = null;
            }
            else
            {
                status = 0;
                type = SignalMessageType.Incoming;
                author = SignalDBContext.GetOrCreateContactLocked(envelope.getSource(), timestamp, this);
            }

            List<SignalAttachment> attachments = new List<SignalAttachment>();
            SignalMessage message = new SignalMessage()
            {
                Type = type,
                Status = status,
                Author = author,
                Content = new SignalMessageContent() { Content = body },
                ThreadId = threadId,
                DeviceId = (uint)envelope.getSourceDevice(),
                Receipts = 0,
                ComposedTimestamp = composedTimestamp,
                ReceivedTimestamp = timestamp,
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
                        SentFileName = pointer.FileName,
                        ContentType = "",
                        Key = pointer.getKey(),
                        Relay = pointer.getRelay(),
                        StorageId = pointer.getId()
                    };
                    attachments.Add(sa);
                }
            }
            Debug.WriteLine("received message: " + message.Content);
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await UIHandleIncomingMessage(message);
            }).AsTask().Wait();
        }
    }
}