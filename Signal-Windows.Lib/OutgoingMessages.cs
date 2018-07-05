using libsignal;
using libsignalservice;
using libsignalservice.crypto;
using libsignalservice.messages;
using libsignalservice.messages.multidevice;
using libsignalservice.push;
using libsignalservice.push.exceptions;
using libsignalservice.util;
using Microsoft.Extensions.Logging;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Signal_Windows.Lib
{
    interface ISendable
    {
        SignalMessageStatus Status { set; }
        Task Send(SignalServiceMessageSender messageSender, CancellationToken token);
    }

    class SignalServiceSyncMessageSendable : ISendable
    {
        public SignalMessageStatus Status { get; set; }
        private readonly SignalServiceSyncMessage SyncMessage;

        public SignalServiceSyncMessageSendable(SignalServiceSyncMessage message)
        {
            SyncMessage = message;
        }

        public async Task Send(SignalServiceMessageSender messageSender, CancellationToken token)
        {
            await messageSender.SendMessage(token, SyncMessage);
        }
    }

    class SignalServiceDataMessageSendable : ISendable
    {
        public SignalMessageStatus Status { get; set; }
        private readonly SignalServiceDataMessage DataMessage;
        private readonly SignalServiceAddress Recipient;

        public SignalServiceDataMessageSendable(SignalServiceDataMessage dataMessage, SignalServiceAddress recipient)
        {
            DataMessage = dataMessage;
            Recipient = recipient;
        }

        public async Task Send(SignalServiceMessageSender messageSender, CancellationToken token)
        {
            await messageSender.SendMessage(token, Recipient, DataMessage);
        }
    }

    class SignalMessageSendable : ISendable
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<SignalMessageSendable>();
        public readonly SignalMessage OutgoingSignalMessage;

        public SignalMessageSendable(SignalMessage message)
        {
            OutgoingSignalMessage = message;
        }

        public SignalMessageStatus Status { set => OutgoingSignalMessage.Status = value; }

        public async Task Send(SignalServiceMessageSender messageSender, CancellationToken token)
        {
            List<SignalServiceAttachment> outgoingAttachmentsList = null;
            if (OutgoingSignalMessage.Attachments != null && OutgoingSignalMessage.Attachments.Count > 0)
            {
                outgoingAttachmentsList = new List<SignalServiceAttachment>();
                foreach (var attachment in OutgoingSignalMessage.Attachments)
                {
                    try
                    {
                        var file = await ApplicationData.Current.LocalCacheFolder.GetFileAsync(@"Attachments\" + attachment.Id + ".plain");
                        var stream = await file.OpenStreamForReadAsync();
                        outgoingAttachmentsList.Add(SignalServiceAttachment.NewStreamBuilder()
                            .WithContentType(attachment.ContentType)
                            .WithStream(stream)
                            .WithLength(stream.Length)
                            .WithFileName(attachment.SentFileName)
                            .Build());
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"HandleOutgoingMessages() failed to add attachment {attachment.Id}: {e.Message}\n{e.StackTrace}");
                    }
                }
            }

            SignalServiceDataMessage message = new SignalServiceDataMessage()
            {
                Body = OutgoingSignalMessage.Content.Content,
                Timestamp = OutgoingSignalMessage.ComposedTimestamp,
                ExpiresInSeconds = (int)OutgoingSignalMessage.ExpiresAt,
                Attachments = outgoingAttachmentsList
            };

            if (!OutgoingSignalMessage.ThreadId.EndsWith("="))
            {
                if (!token.IsCancellationRequested)
                {
                    await messageSender.SendMessage(token, new SignalServiceAddress(OutgoingSignalMessage.ThreadId), message);
                    OutgoingSignalMessage.Status = SignalMessageStatus.Confirmed;
                }
            }
            else
            {
                List<SignalServiceAddress> recipients = new List<SignalServiceAddress>();
                SignalGroup g = await SignalDBContext.GetOrCreateGroupLocked(OutgoingSignalMessage.ThreadId, 0);
                foreach (GroupMembership sc in g.GroupMemberships)
                {
                    if (sc.Contact.ThreadId != SignalLibHandle.Instance.Store.Username)
                    {
                        recipients.Add(new SignalServiceAddress(sc.Contact.ThreadId));
                    }
                }
                message.Group = new SignalServiceGroup()
                {
                    GroupId = Base64.Decode(g.ThreadId),
                    Type = SignalServiceGroup.GroupType.DELIVER
                };
                if (!token.IsCancellationRequested)
                {
                    await messageSender.SendMessage(token, recipients, message);
                    OutgoingSignalMessage.Status = SignalMessageStatus.Confirmed;
                }
            }
        }
    }

    class OutgoingMessages
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<OutgoingMessages>();
        private readonly CancellationToken Token;
        private readonly SignalLibHandle Handle;
        private readonly SignalStore Store;
        private readonly Task<SignalServiceMessagePipe> CreatePipeTask;

        public OutgoingMessages(CancellationToken token, Task<SignalServiceMessagePipe> createPipeTask, SignalStore store, SignalLibHandle handle)
        {
            Token = token;
            CreatePipeTask = createPipeTask;
            Store = store;
            Handle = handle;
        }

        public async Task HandleOutgoingMessages()
        {
            Logger.LogDebug("HandleOutgoingMessages()");
            var messageSender = new SignalServiceMessageSender(Token, LibUtils.ServiceConfiguration, Store.Username, Store.Password, (int)Store.DeviceId, new Store(), await CreatePipeTask, null, LibUtils.USER_AGENT);
            while (!Token.IsCancellationRequested)
            {
                ISendable sendable = null;
                try
                {
                    sendable = Handle.OutgoingQueue.Take(Token);
                    await sendable.Send(messageSender, Token);
                }
                catch (OperationCanceledException)
                {
                    Logger.LogInformation("HandleOutgoingMessages() finished");
                    return;
                }
                catch (EncapsulatedExceptions exceptions)
                {
                    sendable.Status = SignalMessageStatus.Confirmed;
                    Logger.LogError("HandleOutgoingMessages() encountered libsignal exceptions");
                    IList<UntrustedIdentityException> identityExceptions = exceptions.UntrustedIdentityExceptions;
                    if (exceptions.NetworkExceptions.Count > 0)
                    {
                        sendable.Status = SignalMessageStatus.Failed_Network;
                    }
                    if (identityExceptions.Count > 0)
                    {
                        sendable.Status = SignalMessageStatus.Failed_Identity;
                    }
                    foreach (UntrustedIdentityException e in identityExceptions)
                    {
                        await Handle.HandleOutgoingKeyChangeLocked(e.E164number, Base64.EncodeBytes(e.IdentityKey.serialize()));
                    }
                }
                catch (RateLimitException)
                {
                    Logger.LogError("HandleOutgoingMessages() could not send due to rate limits");
                    sendable.Status = SignalMessageStatus.Failed_Ratelimit;
                }
                catch (UntrustedIdentityException e)
                {
                    Logger.LogError("HandleOutgoingMessages() could not send due to untrusted identities");
                    sendable.Status = SignalMessageStatus.Failed_Identity;
                    await Handle.HandleOutgoingKeyChangeLocked(e.E164number, Base64.EncodeBytes(e.IdentityKey.serialize()));
                }
                catch (Exception e)
                {
                    var line = new StackTrace(e, true).GetFrames()[0].GetFileLineNumber();
                    Logger.LogError("HandleOutgoingMessages() failed in line {0}: {1}\n{2}", line, e.Message, e.StackTrace);
                    sendable.Status = SignalMessageStatus.Failed_Unknown;
                }
                await Handle.HandleMessageSentLocked(sendable);
            }
            Logger.LogInformation("HandleOutgoingMessages() finished");
        }
    }
}
