using libsignal.messages.multidevice;
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using static libsignalservice.SignalServiceMessagePipe;

namespace Signal_Windows.Lib
{
    class IncomingMessages : IMessagePipeCallback
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<IncomingMessages>();
        private readonly CancellationToken Token;
        private readonly SignalServiceMessagePipe Pipe;
        private readonly SignalServiceMessageReceiver MessageReceiver;

        public IncomingMessages(CancellationToken token, SignalServiceMessagePipe pipe, SignalServiceMessageReceiver messageReceiver)
        {
            Token = token;
            Pipe = pipe;
            MessageReceiver = messageReceiver;
        }

        public async Task HandleIncomingMessages()
        {
            Logger.LogDebug("HandleIncomingMessages()");
            while (!Token.IsCancellationRequested)
            {
                try
                {
                    await Pipe.ReadBlocking(this);
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

        public async Task OnMessage(SignalServiceMessagePipeMessage message)
        {
            Logger.LogTrace("OnMessage() locking");
            await SignalLibHandle.Instance.SemaphoreSlim.WaitAsync(Token);
            Logger.LogTrace("OnMessage() locked");
            try
            {
                if (message is SignalServiceEnvelope envelope)
                {
                    List<SignalMessage> messages = new List<SignalMessage>();
                    if (envelope.IsReceipt())
                    {
                        SignalMessage update = SignalDBContext.IncreaseReceiptCountLocked(envelope);
                        if (update != null)
                        {
                            await SignalLibHandle.Instance.DispatchMessageUpdate(update);
                        }
                    }
                    else if (envelope.IsPreKeySignalMessage() || envelope.IsSignalMessage())
                    {
                        await HandleMessage(envelope);
                    }
                    else
                    {
                        Logger.LogWarning("OnMessage() could not handle unknown message type {0}", envelope.GetEnvelopeType());
                    }
                }
                else if (message is SignalServiceMessagePipeEmptyMessage)
                {
                    SignalLibHandle.Instance.DispatchPipeEmptyMessage();
                }
            }
            finally
            {
                SignalLibHandle.Instance.SemaphoreSlim.Release();
                Logger.LogTrace("OnMessage() released");
            }
        }

        private async Task HandleMessage(SignalServiceEnvelope envelope)
        {
            var cipher = new SignalServiceCipher(new SignalServiceAddress(SignalLibHandle.Instance.Store.Username), new Store());
            var content = cipher.Decrypt(envelope);
            long timestamp = Util.CurrentTimeMillis();

            if (content.Message != null)
            {
                SignalServiceDataMessage message = content.Message;
                if (message.EndSession)
                {
                    await HandleSessionResetMessage(envelope, content, message, false, timestamp);
                }
                else if (message.IsGroupUpdate())
                {
                    if (message.Group.Type == SignalServiceGroup.GroupType.UPDATE)
                    {
                        await HandleGroupUpdateMessage(envelope, content, message, false, timestamp);
                    }
                    else if (message.Group.Type == SignalServiceGroup.GroupType.QUIT)
                    {
                        await HandleGroupLeaveMessage(envelope, content, message, false, timestamp);
                    }
                    else if (message.Group.Type == SignalServiceGroup.GroupType.REQUEST_INFO)
                    {
                        Logger.LogWarning("Received REQUEST_INFO request");
                    }
                }
                else if (message.ExpirationUpdate)
                {
                    await HandleExpirationUpdateMessage(envelope, content, message, false, timestamp);
                }
                else
                {
                    await HandleSignalMessage(envelope, content, message, false, timestamp);
                }
            }
            else if (content.SynchronizeMessage != null)
            {
                if (content.SynchronizeMessage.Sent != null)
                {
                    var syncMessage = content.SynchronizeMessage.Sent;
                    var dataMessage = syncMessage.Message;

                    if (dataMessage.EndSession)
                    {
                        await HandleSessionResetMessage(envelope, content, dataMessage, true, timestamp);
                    }
                    else if (dataMessage.IsGroupUpdate())
                    {
                        if (dataMessage.Group.Type == SignalServiceGroup.GroupType.UPDATE)
                        {
                            await HandleGroupUpdateMessage(envelope, content, dataMessage, true, timestamp);
                        }
                        else if (dataMessage.Group.Type == SignalServiceGroup.GroupType.QUIT)
                        {
                            await HandleGroupLeaveMessage(envelope, content, dataMessage, true, timestamp);
                        }
                        else if (dataMessage.Group.Type == SignalServiceGroup.GroupType.REQUEST_INFO)
                        {
                            Logger.LogWarning("Received synced REQUEST_INFO request");
                        }
                    }
                    else if (dataMessage.ExpirationUpdate)
                    {
                        await HandleExpirationUpdateMessage(envelope, content, dataMessage, true, timestamp);
                    }
                    else
                    {
                        await HandleSignalMessage(envelope, content, dataMessage, true, timestamp);
                    }
                }
                else if (content.SynchronizeMessage.Reads != null)
                {
                    var readMessages = content.SynchronizeMessage.Reads;
                    foreach (var readMessage in readMessages)
                    {
                        try
                        {
                            await HandleSyncedReadMessage(readMessage);
                        }
                        catch (Exception e)
                        {
                            Logger.LogError("HandleReadMessage failed: {0}\n{1}", e.Message, e.StackTrace);
                        }
                    }
                }
                else if (content.SynchronizeMessage.BlockedList != null)
                {
                    List<string> blockedNumbers = content.SynchronizeMessage.BlockedList.Numbers;
                    await HandleBlockedNumbers(blockedNumbers);
                }
                else if (content.SynchronizeMessage.Groups != null)
                {
                    Logger.LogInformation("HandleMessage() handling groups sync message from device {0}", envelope.GetSourceDevice());
                    int read;
                    var avatarBuffer = new byte[4096];
                    var groups = content.SynchronizeMessage.Groups;
                    using (var tmpFile = LibUtils.CreateTmpFile("groups_sync"))
                    {
                        var plaintextStream = await MessageReceiver.RetrieveAttachment(Token, groups.AsPointer(), tmpFile, 10000, null);
                        var deviceGroupsStream = new DeviceGroupsInputStream(plaintextStream);
                        var groupsList = new List<(SignalGroup, IList<string>)>();
                        DeviceGroup g;
                        while ((g = deviceGroupsStream.Read()) != null)
                        {
                            if (g.Avatar != null)
                            {
                                SignalServiceAttachmentStream ssas = g.Avatar.AsStream();
                                while ((read = ssas.InputStream.Read(avatarBuffer, 0, avatarBuffer.Length)) > 0)
                                {

                                }
                            }
                            var group = new SignalGroup()
                            {
                                ThreadDisplayName = g.Name,
                                ThreadId = Base64.EncodeBytes(g.Id),
                                GroupMemberships = new List<GroupMembership>(),
                                CanReceive = true,
                                ExpiresInSeconds = g.ExpirationTimer != null ? g.ExpirationTimer.Value : 0
                            };
                            groupsList.Add((group, g.Members));
                        }
                        List<SignalConversation> newConversations = await SignalDBContext.InsertOrUpdateGroups(groupsList);
                        await SignalLibHandle.Instance.DispatchAddOrUpdateConversations(newConversations);
                    }
                }
                else if (content.SynchronizeMessage.Contacts != null && content.SynchronizeMessage.Contacts.Complete) //TODO incomplete updates
                {
                    Logger.LogInformation("HandleMessage() handling contacts sync message from device {0}", envelope.GetSourceDevice());
                    int read;
                    var avatarBuffer = new byte[4096];
                    ContactsMessage contacts = content.SynchronizeMessage.Contacts;
                    using (var tmpFile = LibUtils.CreateTmpFile("contacts_sync"))
                    {
                        var plaintextStream = await MessageReceiver.RetrieveAttachment(Token, contacts.Contacts.AsPointer(), tmpFile, 10000, null);
                        var deviceContactsStream = new DeviceContactsInputStream(plaintextStream);
                        List<SignalContact> contactsList = new List<SignalContact>();
                        DeviceContact c;
                        while ((c = deviceContactsStream.Read()) != null)
                        {
                            if (c.Avatar != null)
                            {
                                SignalServiceAttachmentStream ssas = c.Avatar.AsStream();
                                while ((read = ssas.InputStream.Read(avatarBuffer, 0, avatarBuffer.Length)) > 0)
                                {

                                }
                            }
                            SignalContact contact = new SignalContact()
                            {
                                ThreadDisplayName = c.Name,
                                ThreadId = c.Number,
                                Color = c.Color,
                                CanReceive = true,
                                ExpiresInSeconds = c.ExpirationTimer != null ? c.ExpirationTimer.Value : 0
                            };
                            contactsList.Add(contact);
                        }
                        var newConversations = SignalDBContext.InsertOrUpdateContacts(contactsList);
                        await SignalLibHandle.Instance.DispatchAddOrUpdateConversations(newConversations);
                    }
                }
            }
            else if (content.ReadMessage != null)
            {
                SignalServiceReceiptMessage receiptMessage = content.ReadMessage;
                Logger.LogTrace("HandleMessage() received ReceiptMessage (type={0}, when={1})", receiptMessage.ReceiptType, receiptMessage.When);
            }
            else
            {
                //TODO callmessages
                Logger.LogWarning("HandleMessage() received unrecognized message");
            }
        }

        private async Task HandleSyncedReadMessage(ReadMessage readMessage)
        {
            var conv = await SignalDBContext.UpdateMessageRead(readMessage);
            await SignalLibHandle.Instance.DispatchMessageRead(conv.LastSeenMessageIndex, conv);
        }

        private async Task HandleExpirationUpdateMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage message, bool isSync, long timestamp)
        {
            SignalMessageDirection type;
            SignalContact author;
            SignalMessageStatus status;
            string prefix;
            SignalConversation conversation;
            long composedTimestamp;

            if (isSync)
            {
                var sent = content.SynchronizeMessage.Sent;
                type = SignalMessageDirection.Synced;
                status = SignalMessageStatus.Confirmed;
                composedTimestamp = sent.Timestamp;
                author = null;
                prefix = "You have";
                if (message.Group != null)
                {
                    conversation = await SignalDBContext.GetOrCreateGroupLocked(Base64.EncodeBytes(message.Group.GroupId), 0);
                }
                else
                {
                    conversation = await SignalDBContext.GetOrCreateContactLocked(sent.Destination.ForceGetValue(), 0);
                }
            }
            else
            {
                status = 0;
                type = SignalMessageDirection.Incoming;
                author = await SignalDBContext.GetOrCreateContactLocked(envelope.GetSource(), timestamp);
                prefix = $"{author.ThreadDisplayName} has";
                composedTimestamp = envelope.GetTimestamp();
                if (message.Group != null)
                {
                    conversation = await SignalDBContext.GetOrCreateGroupLocked(Base64.EncodeBytes(message.Group.GroupId), 0);
                }
                else
                {
                    conversation = await SignalDBContext.GetOrCreateContactLocked(envelope.GetSource(), 0);
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
                DeviceId = (uint)envelope.GetSourceDevice(),
                Receipts = 0,
                ComposedTimestamp = composedTimestamp,
                ReceivedTimestamp = timestamp,
            };
            await SignalLibHandle.Instance.SaveAndDispatchSignalMessage(sm, conversation);
        }

        private async Task HandleSessionResetMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage, bool isSync, long timestamp)
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
                var sent = content.SynchronizeMessage.Sent;
                type = SignalMessageDirection.Synced;
                status = SignalMessageStatus.Confirmed;
                composedTimestamp = sent.Timestamp;
                author = null;
                prefix = "You have";
                conversationId = sent.Destination.ForceGetValue();
            }
            else
            {
                status = 0;
                type = SignalMessageDirection.Incoming;
                author = await SignalDBContext.GetOrCreateContactLocked(envelope.GetSource(), timestamp);
                prefix = $"{author.ThreadDisplayName} has";
                composedTimestamp = envelope.GetTimestamp();
                conversationId = envelope.GetSource();
            }
            LibsignalDBContext.DeleteAllSessions(conversationId);
            conversation = await SignalDBContext.GetOrCreateContactLocked(conversationId, 0);

            SignalMessage sm = new SignalMessage()
            {
                Direction = type,
                Type = SignalMessageType.SessionReset,
                Status = status,
                Author = author,
                Content = new SignalMessageContent() { Content = $"{prefix} reset the session." },
                ThreadId = conversationId,
                DeviceId = (uint)envelope.GetSourceDevice(),
                Receipts = 0,
                ComposedTimestamp = composedTimestamp,
                ReceivedTimestamp = timestamp,
            };
            await SignalLibHandle.Instance.SaveAndDispatchSignalMessage(sm, conversation);
        }

        /// <summary>
        /// Handles a list of blocked numbers. This will update the database to match the
        /// blocked numbers list.
        /// </summary>
        /// <param name="blockedNumbers">The list of blocked numbers.</param>
        private async Task HandleBlockedNumbers(List<string> blockedNumbers)
        {
            List<SignalContact> blockedContacts = new List<SignalContact>();
            List<SignalContact> contacts = SignalDBContext.GetAllContactsLocked();
            foreach (var contact in contacts)
            {
                if (blockedNumbers.Contains(contact.ThreadId))
                {
                    if (!contact.Blocked)
                    {
                        contact.Blocked = true;
                        SignalDBContext.UpdateBlockStatus(contact);
                        blockedContacts.Add(contact);
                    }
                }
                else
                {
                    if (contact.Blocked)
                    {
                        contact.Blocked = false;
                        SignalDBContext.UpdateBlockStatus(contact);
                    }
                }
            }
            await SignalLibHandle.Instance.DispatchHandleBlockedContacts(blockedContacts);
        }

        private async Task HandleGroupLeaveMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage, bool isSync, long timestamp)
        {
            SignalServiceGroup sentGroup = dataMessage.Group;
            if (sentGroup != null)
            {
                string groupid = Base64.EncodeBytes(sentGroup.GroupId);
                SignalGroup group = await SignalDBContext.GetOrCreateGroupLocked(groupid, 0);
                if (isSync)
                {
                    SignalContact author = await SignalDBContext.GetOrCreateContactLocked(envelope.GetSource(), 0);
                    SignalMessage sm = new SignalMessage()
                    {
                        Direction = SignalMessageDirection.Incoming,
                        Type = SignalMessageType.GroupLeave,
                        Status = SignalMessageStatus.Received,
                        Author = author,
                        Content = new SignalMessageContent() { Content = $"You have left the group." },
                        ThreadId = groupid,
                        DeviceId = (uint)envelope.GetSourceDevice(),
                        Receipts = 0,
                        ComposedTimestamp = envelope.GetTimestamp(),
                        ReceivedTimestamp = timestamp,
                    };
                    SignalConversation updatedConversation = SignalDBContext.RemoveMemberFromGroup(groupid, author, sm);
                    await SignalLibHandle.Instance.DispatchAddOrUpdateConversation(updatedConversation, sm);
                }
                else
                {
                    SignalContact author = await SignalDBContext.GetOrCreateContactLocked(envelope.GetSource(), 0);
                    SignalMessage sm = new SignalMessage()
                    {
                        Direction = SignalMessageDirection.Incoming,
                        Type = SignalMessageType.GroupLeave,
                        Status = SignalMessageStatus.Received,
                        Author = author,
                        Content = new SignalMessageContent() { Content = $"{author.ThreadDisplayName} has left the group." },
                        ThreadId = groupid,
                        DeviceId = (uint)envelope.GetSourceDevice(),
                        Receipts = 0,
                        ComposedTimestamp = envelope.GetTimestamp(),
                        ReceivedTimestamp = timestamp,
                    };
                    SignalConversation updatedConversation = SignalDBContext.RemoveMemberFromGroup(groupid, author, sm);
                    await SignalLibHandle.Instance.DispatchAddOrUpdateConversation(updatedConversation, sm);
                }
            }
            else
            {
                Logger.LogError("HandleGroupLeaveMessage() received group update without group info");
            }
        }

        private async Task HandleGroupUpdateMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage, bool isSync, long timestamp)
        {
            if (dataMessage.Group != null) //TODO check signal droid: group messages have different types!
            {
                SignalServiceGroup group = dataMessage.Group;
                string groupid = Base64.EncodeBytes(group.GroupId);
                SignalGroup g = new SignalGroup();
                string displayname = "Unknown group";
                string avatarfile = null;
                if (group.Name != null)
                {
                    displayname = group.Name;
                }
                var dbgroup = SignalDBContext.InsertOrUpdateGroupLocked(groupid, displayname, avatarfile, true, 0, timestamp);
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
                    var sent = content.SynchronizeMessage.Sent;
                    type = SignalMessageDirection.Synced;
                    status = SignalMessageStatus.Confirmed;
                    composedTimestamp = sent.Timestamp;
                    author = null;
                    prefix = "You have";
                }
                else
                {
                    status = 0;
                    type = SignalMessageDirection.Incoming;
                    author = await SignalDBContext.GetOrCreateContactLocked(envelope.GetSource(), timestamp);
                    prefix = $"{author.ThreadDisplayName} has";
                    composedTimestamp = envelope.GetTimestamp();
                }

                SignalMessage sm = new SignalMessage()
                {
                    Direction = type,
                    Type = SignalMessageType.GroupUpdate,
                    Status = status,
                    Author = author,
                    Content = new SignalMessageContent() { Content = $"{prefix} updated the group." },
                    ThreadId = groupid,
                    DeviceId = (uint)envelope.GetSourceDevice(),
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
                await SignalLibHandle.Instance.DispatchAddOrUpdateConversation(dbgroup, sm);
            }
            else
            {
                Logger.LogError("HandleGroupUpdateMessage() received group update without group info");
            }
        }

        private async Task HandleSignalMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage, bool isSync, long timestamp)
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
                var threadId = Base64.EncodeBytes(rawId);
                conversation = await SignalDBContext.GetOrCreateGroupLocked(threadId, timestamp);
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
                    await SignalLibHandle.Instance.OutgoingMessages.SendMessage(envelope.GetSourceAddress(), requestInfoMessage);
                }
                composedTimestamp = envelope.GetTimestamp();
            }
            else
            {
                if (isSync)
                {
                    var sent = content.SynchronizeMessage.Sent;
                    conversation = await SignalDBContext.GetOrCreateContactLocked(sent.Destination.ForceGetValue(), timestamp);
                    composedTimestamp = sent.Timestamp;
                }
                else
                {
                    conversation = await SignalDBContext.GetOrCreateContactLocked(envelope.GetSource(), timestamp);
                    composedTimestamp = envelope.GetTimestamp();
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
                author = await SignalDBContext.GetOrCreateContactLocked(envelope.GetSource(), timestamp);
            }

            if (author != null && author.Blocked)
            {
                // Don't save blocked messages
                return;
            }

            List<SignalAttachment> attachments = new List<SignalAttachment>();
            SignalMessage message = new SignalMessage()
            {
                Direction = type,
                Status = status,
                Author = author,
                Content = new SignalMessageContent() { Content = body },
                ThreadId = conversation.ThreadId,
                DeviceId = (uint)envelope.GetSourceDevice(),
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
                    var pointer = receivedAttachment.AsPointer();
                    SignalAttachment sa = new SignalAttachment()
                    {
                        Message = message,
                        Status = (uint)SignalAttachmentStatus.Default,
                        SentFileName = pointer.FileName,
                        ContentType = receivedAttachment.ContentType,
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
            await SignalLibHandle.Instance.SaveAndDispatchSignalMessage(message, conversation);
        }
    }
}
