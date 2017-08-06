using libsignalservice.messages;
using libsignalservice.push;
using libsignalservice.util;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Signal_Windows.ViewModels
{
    public partial class MainPageViewModel
    {
        /// <summary>
        /// Queue for pending outgoing messages.
        /// </summary>
        private BlockingCollection<SignalMessage> OutgoingQueue = new BlockingCollection<SignalMessage>(new ConcurrentQueue<SignalMessage>());

        /// <summary>
        /// Reads pending messages from the <see cref="OutgoingQueue"/> and attempts to send them
        /// </summary>
        public void HandleOutgoingMessages()
        {
            Debug.WriteLine("HandleOutgoingMessages starting...");
            CancellationToken token = CancelSource.Token;
            while (!token.IsCancellationRequested)
            {
                SignalMessage outgoingSignalMessage = null;
                try
                {
                    outgoingSignalMessage = OutgoingQueue.Take(token);
                    SignalServiceDataMessage message = new SignalServiceDataMessage()
                    {
                        Body = outgoingSignalMessage.Content.Content,
                        Timestamp = outgoingSignalMessage.ComposedTimestamp,
                        ExpiresInSeconds = (int)outgoingSignalMessage.ExpiresAt
                    };

                    if (outgoingSignalMessage.ThreadId[0] == '+')
                    {
                        if (!token.IsCancellationRequested)
                        {
                            MessageSender.sendMessage(new SignalServiceAddress(outgoingSignalMessage.ThreadId), message);
                            SignalDBContext.UpdateMessageStatus(outgoingSignalMessage, this);
                        }
                    }
                    else
                    {
                        List<SignalServiceAddress> recipients = new List<SignalServiceAddress>();
                        SignalGroup g = (SignalGroup)SelectedThread;
                        foreach (GroupMembership sc in g.GroupMemberships)
                        {
                            if (sc.Contact.ThreadId != App.Store.Username)
                            {
                                recipients.Add(new SignalServiceAddress(sc.Contact.ThreadId));
                            }
                        }
                        message.Group = new SignalServiceGroup()
                        {
                            GroupId = Base64.decode(g.ThreadId),
                            Type = SignalServiceGroup.GroupType.DELIVER
                        };
                        if (!token.IsCancellationRequested)
                        {
                            MessageSender.sendMessage(recipients, message);
                            SignalDBContext.UpdateMessageStatus(outgoingSignalMessage, this);
                        }
                    }
                }
                catch (OperationCanceledException e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine(e.StackTrace);
                }
                catch (libsignal.exceptions.UntrustedIdentityException e)
                {
                    LibsignalDBContext.UpdateIdentityLocked(e.getName(), Base64.encodeBytes(e.getUntrustedIdentity().serialize()), VerifiedStatus.Default, this);
                    //TODO devise appropriate resend strategy
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine(e.StackTrace);
                    OutgoingQueue.Add(outgoingSignalMessage);
                    //TODO notify UI
                }
            }
            Debug.WriteLine("HandleOutgoingMessages finished");
        }
    }
}