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
            var cipher = new SignalServiceCipher(new SignalServiceAddress(App.Store.Username), new Store());
            var content = cipher.decrypt(envelope);
            long timestamp = Util.CurrentTimeMillis();

            if (content.Message != null)
            {
                SignalServiceDataMessage message = content.Message;
                if (message.EndSession)
                {
                    LibsignalDBContext.DeleteAllSessions(envelope.getSource());
                }
                else if (message.IsGroupUpdate())
                {
                    if (message.Group.Type == SignalServiceGroup.GroupType.UPDATE)
                    {
                        HandleGroupUpdateMessage(envelope, content, message, timestamp);
                    }
                }
                else if (message.ExpirationUpdate)
                {
                    string threadid;
                    if (message.Group != null)
                    {
                        threadid = Base64.encodeBytes(message.Group.GroupId);
                        SignalDBContext.GetOrCreateGroupLocked(threadid, timestamp, this);
                    }
                    else
                    {
                        threadid = envelope.getSource();
                        SignalDBContext.GetOrCreateContactLocked(threadid, timestamp, this);
                    }
                    SignalDBContext.UpdateExpiresInLocked(new SignalConversation() { ThreadId = threadid }, (uint)message.ExpiresInSeconds);
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
                    if (dataMessage.IsGroupUpdate())
                    {
                        if (dataMessage.Group.Type == SignalServiceGroup.GroupType.UPDATE)
                        {
                            HandleGroupUpdateMessage(envelope, content, dataMessage, timestamp);
                        }
                    }
                    else
                    {
                        HandleSignalMessage(envelope, content, dataMessage, true, timestamp);
                    }
                }
                else if (content.SynchronizeMessage.getRead().HasValue)
                {
                    var readMessages = content.SynchronizeMessage.getRead().ForceGetValue();
                    foreach (var readMessage in readMessages)
                    {
                        //TODO
                    }
                }
            } //TODO callmessages
            else
            {
                Debug.WriteLine("HandleMessage got unrecognized message from " + envelope.getSource());
            }
        }

        private void HandleGroupUpdateMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage, long timestamp)
        {
            if (dataMessage.Group != null) //TODO check signal droid: group messages have different types!
            {
                SignalServiceGroup group = dataMessage.Group;
                SignalGroup g = new SignalGroup();
                string displayname = "Unknown group";
                string avatarfile = null;
                if (group.Name != null)
                {
                    displayname = group.Name;
                }
                var dbgroup = SignalDBContext.InsertOrUpdateGroupLocked(Base64.encodeBytes(group.GroupId), displayname, avatarfile, true, timestamp, this);
                if (group.Members != null)
                {
                    foreach (var member in group.Members)
                    {
                        SignalDBContext.InsertOrUpdateGroupMembershipLocked(dbgroup.Id, SignalDBContext.GetOrCreateContactLocked(member, 0, this).Id);
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
            SignalMessageDirection type;
            SignalContact author;
            SignalMessageStatus status;
            string threadId;
            long composedTimestamp;
            string body = dataMessage.Body != null ? dataMessage.Body : "";

            if (dataMessage.Group != null)
            {
                var rawId = dataMessage.Group.GroupId;
                threadId = Base64.encodeBytes(rawId);
                var g = SignalDBContext.GetOrCreateGroupLocked(threadId, timestamp, this);
                if (!g.CanReceive)
                {
                    SignalServiceGroup group = new SignalServiceGroup()
                    {
                        Type = SignalServiceGroup.GroupType.REQUEST_INFO,
                        GroupId = rawId
                    };
                    SignalServiceDataMessage requestInfoMessage = new SignalServiceDataMessage()
                    {
                        Group = group,
                        Timestamp = Util.CurrentTimeMillis()
                    };
                    MessageSender.sendMessage(envelope.getSourceAddress(), requestInfoMessage);
                }
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
                type = SignalMessageDirection.Synced;
                status = SignalMessageStatus.Confirmed;
                author = null;
            }
            else
            {
                status = 0;
                type = SignalMessageDirection.Incoming;
                author = SignalDBContext.GetOrCreateContactLocked(envelope.getSource(), timestamp, this);
            }

            List<SignalAttachment> attachments = new List<SignalAttachment>();
            SignalMessage message = new SignalMessage()
            {
                Direction = type,
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
            if (dataMessage.Attachments != null)
            {
                var receivedAttachments = dataMessage.Attachments;
                foreach (var receivedAttachment in receivedAttachments)
                {
                    var pointer = receivedAttachment.asPointer();
                    SignalAttachment sa = new SignalAttachment()
                    {
                        Message = message,
                        Status = (uint)SignalAttachmentStatus.Default,
                        SentFileName = pointer.FileName,
                        ContentType = "",
                        Key = pointer.Key,
                        Relay = pointer.Relay,
                        StorageId = pointer.Id
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