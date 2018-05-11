using libsignalservice;
using libsignalservice.crypto;
using libsignalservice.messages;
using libsignalservice.messages.multidevice;
using libsignalservice.push;
using libsignalservice.util;
using Microsoft.Extensions.Logging;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Strilanc.Value;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static libsignalservice.SignalServiceMessagePipe;

namespace Signal_Windows.Lib
{
    class IncomingMessages : IMessagePipeCallback
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<IncomingMessages>();
        private readonly CancellationToken Token;
        private readonly SignalServiceMessagePipe Pipe;
        private readonly SignalLibHandle Handle;

        public IncomingMessages(CancellationToken token, SignalServiceMessagePipe pipe, SignalLibHandle handle)
        {
            Token = token;
            Pipe = pipe;
            Handle = handle;
        }

        public void HandleIncomingMessages()
        {
            Logger.LogDebug("HandleIncomingMessages()");
            while (!Token.IsCancellationRequested)
            {
                try
                {
                    Pipe.ReadBlocking(this);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    var line = new StackTrace(e, true).GetFrames()[0].GetFileLineNumber();
                    Logger.LogWarning("HandleIncomingMessages() failed: {0} occured ({1}):\n{2}", e.GetType(), e.Message, e.StackTrace);
                }
            }
            Logger.LogInformation("HandleIncomingMessages() finished");
        }

        public void OnMessage(SignalServiceMessagePipeMessage message)
        {
            Logger.LogTrace("OnMessage() locking");
            Handle.SemaphoreSlim.Wait();
            Logger.LogTrace("OnMessage() locked");
            try
            {
                if (message is SignalServiceEnvelope envelope)
                {
                    List<SignalMessage> messages = new List<SignalMessage>();
                    if (envelope.isReceipt())
                    {
                        SignalMessage update = SignalDBContext.IncreaseReceiptCountLocked(envelope);
                        if (update != null)
                        {
                            Handle.DispatchMessageUpdate(update);
                        }
                    }
                    else if (envelope.isPreKeySignalMessage() || envelope.isSignalMessage())
                    {
                        HandleMessage(envelope);
                    }
                    else
                    {
                        Logger.LogWarning("OnMessage() could not handle unknown message type {0}", envelope.getType());
                    }
                }
                else if (message is SignalServiceMessagePipeEmptyMessage)
                {
                    Handle.DispatchPipeEmptyMessage();
                }
            }
            finally
            {
                Handle.SemaphoreSlim.Release();
                Logger.LogTrace("OnMessage() released");
            }
        }

        private void HandleMessage(SignalServiceEnvelope envelope)
        {
            var cipher = new SignalServiceCipher(new SignalServiceAddress(SignalLibHandle.Instance.Store.Username), new Store());
            var content = cipher.decrypt(envelope);
            long timestamp = Util.CurrentTimeMillis();

            if (content.Message != null)
            {
                SignalServiceDataMessage message = content.Message;
                if (message.EndSession)
                {
                    HandleSessionResetMessage(envelope, content, message, false, timestamp);
                }
                else if (message.IsGroupUpdate())
                {
                    if (message.Group.Type == SignalServiceGroup.GroupType.UPDATE)
                    {
                        HandleGroupUpdateMessage(envelope, content, message, false, timestamp);
                    }
                }
                else if (message.ExpirationUpdate)
                {
                    HandleExpirationUpdateMessage(envelope, content, message, false, timestamp);
                }
                else
                {
                    HandleSignalMessage(envelope, content, message, false, timestamp);
                }
            }
            else if (content.SynchronizeMessage != null)
            {
                if (content.SynchronizeMessage.getSent().HasValue)
                {
                    var syncMessage = content.SynchronizeMessage.getSent().ForceGetValue();
                    var dataMessage = syncMessage.getMessage();

                    if (dataMessage.EndSession)
                    {
                        HandleSessionResetMessage(envelope, content, dataMessage, true, timestamp);
                    }
                    else if (dataMessage.IsGroupUpdate())
                    {
                        if (dataMessage.Group.Type == SignalServiceGroup.GroupType.UPDATE)
                        {
                            HandleGroupUpdateMessage(envelope, content, dataMessage, true, timestamp);
                        }
                    }
                    else if (dataMessage.ExpirationUpdate)
                    {
                        HandleExpirationUpdateMessage(envelope, content, dataMessage, true, timestamp);
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
                        try
                        {
                            HandleReadMessage(readMessage);
                        }
                        catch(Exception e)
                        {
                            Logger.LogError("HandleReadMessage failed: {0}\n{1}", e.Message, e.StackTrace);
                        }
                    }
                }
            } //TODO callmessages
            else
            {
                Logger.LogWarning("HandleMessage() received unrecognized message");
            }
        }

        private void HandleReadMessage(ReadMessage readMessage)
        {
            SignalDBContext.UpdateMessageRead(readMessage);
        }

        private void HandleExpirationUpdateMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage message, bool isSync, long timestamp)
        {
            SignalMessageDirection type;
            SignalContact author;
            SignalMessageStatus status;
            string prefix;
            SignalConversation conversation;
            long composedTimestamp;

            if (isSync)
            {
                var sent = content.SynchronizeMessage.getSent().ForceGetValue();
                type = SignalMessageDirection.Synced;
                status = SignalMessageStatus.Confirmed;
                composedTimestamp = sent.getTimestamp();
                author = null;
                prefix = "You have";
                if (message.Group != null)
                {
                    conversation = SignalDBContext.GetOrCreateGroupLocked(Base64.encodeBytes(message.Group.GroupId), 0);
                }
                else
                {
                    conversation = SignalDBContext.GetOrCreateContactLocked(sent.getDestination().ForceGetValue(), 0);
                }
            }
            else
            {
                status = 0;
                type = SignalMessageDirection.Incoming;
                author = SignalDBContext.GetOrCreateContactLocked(envelope.getSource(), timestamp);
                prefix = $"{author.ThreadDisplayName} has";
                composedTimestamp = envelope.getTimestamp();
                if (message.Group != null)
                {
                    conversation = SignalDBContext.GetOrCreateGroupLocked(Base64.encodeBytes(message.Group.GroupId), 0);
                }
                else
                {
                    conversation = SignalDBContext.GetOrCreateContactLocked(envelope.getSource(), 0);
                }
            }
            SignalDBContext.UpdateExpiresInLocked(conversation, (uint)message.ExpiresInSeconds);
            SignalMessage sm = new SignalMessage()
            {
                Direction = type,
                Type = SignalMessageType.ExpireUpdate,
                Status = status,
                Author = author,
                Content = new SignalMessageContent() { Content = $"{prefix} set the expiration timer to {message.ExpiresInSeconds} seconds." },
                ThreadId = conversation.ThreadId,
                DeviceId = (uint)envelope.getSourceDevice(),
                Receipts = 0,
                ComposedTimestamp = composedTimestamp,
                ReceivedTimestamp = timestamp,
            };
            SignalLibHandle.Instance.SaveAndDispatchSignalMessage(sm, conversation);
        }

        private void HandleSessionResetMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage, bool isSync, long timestamp)
        {
            SignalMessageDirection type;
            SignalContact author;
            SignalMessageStatus status;
            SignalConversation conversation;
            string prefix;
            string conversationId;
            long composedTimestamp;

            if (isSync)
            {
                var sent = content.SynchronizeMessage.getSent().ForceGetValue();
                type = SignalMessageDirection.Synced;
                status = SignalMessageStatus.Confirmed;
                composedTimestamp = sent.getTimestamp();
                author = null;
                prefix = "You have";
                conversationId = sent.getDestination().ForceGetValue();
            }
            else
            {
                status = 0;
                type = SignalMessageDirection.Incoming;
                author = SignalDBContext.GetOrCreateContactLocked(envelope.getSource(), timestamp);
                prefix = $"{author.ThreadDisplayName} has";
                composedTimestamp = envelope.getTimestamp();
                conversationId = envelope.getSource();
            }
            LibsignalDBContext.DeleteAllSessions(conversationId);
            conversation = SignalDBContext.GetOrCreateContactLocked(conversationId, 0);

            SignalMessage sm = new SignalMessage()
            {
                Direction = type,
                Type = SignalMessageType.SessionReset,
                Status = status,
                Author = author,
                Content = new SignalMessageContent() { Content = $"{prefix} reset the session." },
                ThreadId = conversationId,
                DeviceId = (uint)envelope.getSourceDevice(),
                Receipts = 0,
                ComposedTimestamp = composedTimestamp,
                ReceivedTimestamp = timestamp,
            };
            SignalLibHandle.Instance.SaveAndDispatchSignalMessage(sm, conversation);
        }

        private void HandleGroupUpdateMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage, bool isSync, long timestamp)
        {
            if (dataMessage.Group != null) //TODO check signal droid: group messages have different types!
            {
                SignalServiceGroup group = dataMessage.Group;
                string groupid = Base64.encodeBytes(group.GroupId);
                SignalGroup g = new SignalGroup();
                string displayname = "Unknown group";
                string avatarfile = null;
                if (group.Name != null)
                {
                    displayname = group.Name;
                }
                var dbgroup = SignalDBContext.InsertOrUpdateGroupLocked(groupid, displayname, avatarfile, true, timestamp);
                if (group.Members != null)
                {
                    foreach (var member in group.Members)
                    {
                        SignalDBContext.InsertOrUpdateGroupMembershipLocked(dbgroup.Id, SignalDBContext.GetOrCreateContactLocked(member, 0).Id);
                    }
                }

                /* insert message into conversation */
                SignalMessageDirection type;
                SignalContact author;
                SignalMessageStatus status;
                string prefix;
                long composedTimestamp;

                if (isSync)
                {
                    var sent = content.SynchronizeMessage.getSent().ForceGetValue();
                    type = SignalMessageDirection.Synced;
                    status = SignalMessageStatus.Confirmed;
                    composedTimestamp = sent.getTimestamp();
                    author = null;
                    prefix = "You have";
                }
                else
                {
                    status = 0;
                    type = SignalMessageDirection.Incoming;
                    author = SignalDBContext.GetOrCreateContactLocked(envelope.getSource(), timestamp);
                    prefix = $"{author.ThreadDisplayName} has";
                    composedTimestamp = envelope.getTimestamp();
                }

                SignalMessage sm = new SignalMessage()
                {
                    Direction = type,
                    Type = SignalMessageType.GroupUpdate,
                    Status = status,
                    Author = author,
                    Content = new SignalMessageContent() { Content = $"{prefix} updated the group." },
                    ThreadId = groupid,
                    DeviceId = (uint)envelope.getSourceDevice(),
                    Receipts = 0,
                    ComposedTimestamp = composedTimestamp,
                    ReceivedTimestamp = timestamp,
                };
                SignalDBContext.SaveMessageLocked(sm);
                dbgroup.MessagesCount += 1;
                if (sm.Direction == SignalMessageDirection.Incoming)
                {
                    dbgroup.UnreadCount += 1;
                }
                else
                {
                    dbgroup.UnreadCount = 0;
                    dbgroup.LastSeenMessageIndex = dbgroup.MessagesCount;
                }
                dbgroup.LastMessage = sm;
                SignalLibHandle.Instance.DispatchAddOrUpdateConversation(dbgroup, sm);
            }
            else
            {
                Logger.LogError("HandleGroupUpdateMessage() received group update without group info");
            }
        }

        private void HandleSignalMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage, bool isSync, long timestamp)
        {
            SignalMessageDirection type;
            SignalContact author;
            SignalMessageStatus status;
            SignalConversation conversation;
            long composedTimestamp;
            string body = dataMessage.Body ?? "";

            if (dataMessage.Group != null)
            {
                var rawId = dataMessage.Group.GroupId;
                var threadId = Base64.encodeBytes(rawId);
                conversation = SignalDBContext.GetOrCreateGroupLocked(threadId, timestamp);
                if (!conversation.CanReceive)
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
                    var list = new List<SignalServiceAddress>();
                    list.Add(new SignalServiceAddress(envelope.getSource()));
                    SignalLibHandle.Instance.OutgoingMessages.SendMessage(list, requestInfoMessage);
                }
                composedTimestamp = envelope.getTimestamp();
            }
            else
            {
                if (isSync)
                {
                    var sent = content.SynchronizeMessage.getSent().ForceGetValue();
                    conversation = SignalDBContext.GetOrCreateContactLocked(sent.getDestination().ForceGetValue(), timestamp);
                    composedTimestamp = sent.getTimestamp();
                }
                else
                {
                    conversation = SignalDBContext.GetOrCreateContactLocked(envelope.getSource(), timestamp);
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
                author = SignalDBContext.GetOrCreateContactLocked(envelope.getSource(), timestamp);
            }

            List<SignalAttachment> attachments = new List<SignalAttachment>();
            SignalMessage message = new SignalMessage()
            {
                Direction = type,
                Status = status,
                Author = author,
                Content = new SignalMessageContent() { Content = body },
                ThreadId = conversation.ThreadId,
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
                        ContentType = receivedAttachment.getContentType(),
                        Key = pointer.Key,
                        Relay = pointer.Relay,
                        StorageId = pointer.Id,
                        Size = (long)pointer.Size,
                        Digest = pointer.Digest
                    };
                    attachments.Add(sa);
                }

                // Make sure to update attachments count
                message.AttachmentsCount = (uint)attachments.Count;
            }
            SignalLibHandle.Instance.SaveAndDispatchSignalMessage(message, conversation);
        }
    }
}
