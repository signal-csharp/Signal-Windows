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

        public void SendMessage(List<SignalServiceAddress> recipients, SignalServiceDataMessage message)
        {
            lock (this)
            {
                MessageSender.SendMessage(recipients, message);
            }
        }

        public void SendMessage(SignalServiceAddress recipient, SignalServiceDataMessage message)
        {
            lock (this)
            {
                MessageSender.SendMessage(recipient, message);
            }
        }

        public void SendMessage(SignalServiceAddress recipient, SignalServiceSyncMessage message)
        {
            lock (this)
            {
                MessageSender.SendMessage(message);
            }
        }

        public void SendMessage(SignalServiceSyncMessage message)
        {
            lock (this)
            {
                MessageSender.SendMessage(message);
            }
        }

        public void HandleOutgoingMessages()
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
                            MessageSender.SendMessage(new SignalServiceAddress(outgoingSignalMessage.ThreadId), message);
                            outgoingSignalMessage.Status = SignalMessageStatus.Confirmed;
                        }
                    }
                    else
                    {
                        List<SignalServiceAddress> recipients = new List<SignalServiceAddress>();
                        SignalGroup g = SignalDBContext.GetOrCreateGroupLocked(outgoingSignalMessage.ThreadId, 0);
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
                            SendMessage(recipients, message);
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
                        Handle.HandleOutgoingKeyChangeLocked(e.E164number, Base64.EncodeBytes(e.IdentityKey.serialize()));
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
                    Handle.HandleOutgoingKeyChangeLocked(e.E164number, Base64.EncodeBytes(e.IdentityKey.serialize()));
                }
                catch (Exception e)
                {
                    var line = new StackTrace(e, true).GetFrames()[0].GetFileLineNumber();
                    Logger.LogError("HandleOutgoingMessages() failed in line {0}: {1}\n{2}", line, e.Message, e.StackTrace);
                    outgoingSignalMessage.Status = SignalMessageStatus.Failed_Unknown;
                }
                Handle.HandleMessageSentLocked(outgoingSignalMessage);
            }
            Logger.LogInformation("HandleOutgoingMessages() finished");
        }
    }
}
