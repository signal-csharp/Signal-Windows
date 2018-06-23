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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Signal_Windows.Lib
{
    class OutgoingMessages
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<IncomingMessages>();
        private readonly CancellationToken Token;
        private readonly SignalServiceMessageSender MessageSender;
        private readonly SignalLibHandle Handle;

        public OutgoingMessages(CancellationToken token, SignalServiceMessageSender sender, SignalLibHandle handle)
        {
            Token = token;
            MessageSender = sender;
            Handle = handle;
        }

        public async Task SendMessage(List<SignalServiceAddress> recipients, SignalServiceDataMessage message)
        {
            await MessageSender.SendMessage(Token, recipients, message);
        }

        public async Task SendMessage(SignalServiceAddress recipient, SignalServiceDataMessage message)
        {
            await MessageSender.SendMessage(Token, recipient, message);
        }

        public void SendMessage(SignalServiceAddress recipient, SignalServiceSyncMessage message)
        {
            lock (this)
            {
                MessageSender.SendMessage(Token, message);
            }
        }

        public void SendMessage(SignalServiceSyncMessage message)
        {
            lock (this)
            {
                MessageSender.SendMessage(Token, message);
            }
        }

        public async Task HandleOutgoingMessages()
        {
            Logger.LogDebug("HandleOutgoingMessages()");
            while (!Token.IsCancellationRequested)
            {
                SignalMessage outgoingSignalMessage = null;
                try
                {
                    outgoingSignalMessage = Handle.OutgoingQueue.Take(Token);
                    SignalServiceDataMessage message = new SignalServiceDataMessage()
                    {
                        Body = outgoingSignalMessage.Content.Content,
                        Timestamp = outgoingSignalMessage.ComposedTimestamp,
                        ExpiresInSeconds = (int)outgoingSignalMessage.ExpiresAt
                    };

                    if (!outgoingSignalMessage.ThreadId.EndsWith("="))
                    {
                        if (!Token.IsCancellationRequested)
                        {
                            await MessageSender.SendMessage(Token, new SignalServiceAddress(outgoingSignalMessage.ThreadId), message);
                            UpdateExpiresAt(outgoingSignalMessage);
                            DisappearingMessagesManager.QueueForDeletion(outgoingSignalMessage);
                            outgoingSignalMessage.Status = SignalMessageStatus.Confirmed;
                        }
                    }
                    else
                    {
                        List<SignalServiceAddress> recipients = new List<SignalServiceAddress>();
                        SignalGroup g = await SignalDBContext.GetOrCreateGroupLocked(outgoingSignalMessage.ThreadId, 0);
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
                        if (!Token.IsCancellationRequested)
                        {
                            await SendMessage(recipients, message);
                            UpdateExpiresAt(outgoingSignalMessage);
                            DisappearingMessagesManager.QueueForDeletion(outgoingSignalMessage);
                            outgoingSignalMessage.Status = SignalMessageStatus.Confirmed;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.LogInformation("HandleOutgoingMessages() finished");
                    return;
                }
                catch (EncapsulatedExceptions exceptions)
                {
                    outgoingSignalMessage.Status = SignalMessageStatus.Confirmed;
                    Logger.LogError("HandleOutgoingMessages() encountered libsignal exceptions");
                    IList<UntrustedIdentityException> identityExceptions = exceptions.UntrustedIdentityExceptions;
                    if (exceptions.NetworkExceptions.Count > 0)
                    {
                        outgoingSignalMessage.Status = SignalMessageStatus.Failed_Network;
                    }
                    if (identityExceptions.Count > 0)
                    {
                        outgoingSignalMessage.Status = SignalMessageStatus.Failed_Identity;
                    }
                    foreach (UntrustedIdentityException e in identityExceptions)
                    {
                        await Handle.HandleOutgoingKeyChangeLocked(e.E164number, Base64.EncodeBytes(e.IdentityKey.serialize()));
                    }
                }
                catch (RateLimitException)
                {
                    Logger.LogError("HandleOutgoingMessages() could not send due to rate limits");
                    outgoingSignalMessage.Status = SignalMessageStatus.Failed_Ratelimit;
                }
                catch (UntrustedIdentityException e)
                {
                    Logger.LogError("HandleOutgoingMessages() could not send due to untrusted identities");
                    outgoingSignalMessage.Status = SignalMessageStatus.Failed_Identity;
                    await Handle.HandleOutgoingKeyChangeLocked(e.E164number, Base64.EncodeBytes(e.IdentityKey.serialize()));
                }
                catch (Exception e)
                {
                    var line = new StackTrace(e, true).GetFrames()[0].GetFileLineNumber();
                    Logger.LogError("HandleOutgoingMessages() failed in line {0}: {1}\n{2}", line, e.Message, e.StackTrace);
                    outgoingSignalMessage.Status = SignalMessageStatus.Failed_Unknown;
                }
                await Handle.HandleMessageSentLocked(outgoingSignalMessage);
            }
            Logger.LogInformation("HandleOutgoingMessages() finished");
        }

        /// <summary>
        /// Updates a message ExpiresAt to be a timestamp instead of a relative value.
        /// </summary>
        /// <param name="message">The message to update</param>
        private void UpdateExpiresAt(SignalMessage message)
        {
            // We update here instead of earlier because we only want to start the timer once the message is actually sent.
            long messageExpiration;
            if (message.ExpiresAt == 0)
            {
                messageExpiration = 0;
            }
            else
            {
                messageExpiration = Util.CurrentTimeMillis() + (long)TimeSpan.FromSeconds(message.ExpiresAt).TotalMilliseconds;
            }
            message.ExpiresAt = messageExpiration;
        }
    }
}
