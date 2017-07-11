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
                    Builder messageBuilder = SignalServiceDataMessage.newBuilder().withBody(outgoingSignalMessage.Content.Content).withTimestamp(outgoingSignalMessage.ComposedTimestamp);
                    List<SignalServiceAddress> recipients = new List<SignalServiceAddress>();
                    if (outgoingSignalMessage.ThreadID[0] == '+')
                    {
                        recipients.Add(new SignalServiceAddress(outgoingSignalMessage.ThreadID));
                    }
                    else
                    {
                        SignalGroup g = (SignalGroup)SelectedThread;
                        foreach (GroupMembership sc in g.GroupMemberships)
                        {
                            recipients.Add(new SignalServiceAddress(sc.Contact.ThreadId));
                        }
                        messageBuilder = messageBuilder.asGroupMessage(new SignalServiceGroup(Base64.decode(g.ThreadId)));
                    }
                    SignalServiceDataMessage ssdm = messageBuilder.build();
                    if (!token.IsCancellationRequested)
                    {
                        SignalManager.sendMessage(recipients, ssdm);
                        SignalDBContext.UpdateMessageLocked(outgoingSignalMessage, this);
                    }
                }
                catch (OperationCanceledException e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine(e.StackTrace);
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
            OutgoingOffSwitch.Set();
        }
    }
}
