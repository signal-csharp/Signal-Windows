using libsignalservice.messages;
using libsignalservice.push;
using Signal_Windows.Models;
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
        public BlockingCollection<SignalMessage> OutgoingQueue = new BlockingCollection<SignalMessage>(new ConcurrentQueue<SignalMessage>());

        /// <summary>
        /// Reads pending messages from the <see cref="OutgoingQueue"/> and attempts to send them
        /// </summary>
        public void HandleOutgoingMessages()
        {
            Debug.WriteLine("HandleOutgoingMessages starting...");
            CancellationToken token = CancelSource.Token;
            while (Running)
            {
                while (!token.IsCancellationRequested)
                {
                    var t = OutgoingQueue.Take(CancelSource.Token);
                    try
                    {
                        Builder messageBuilder = SignalServiceDataMessage.newBuilder().withBody(t.Content).withTimestamp(t.ComposedTimestamp);
                        List<SignalServiceAddress> recipients = new List<SignalServiceAddress>();
                        if (t.ThreadID[0] == '+')
                        {
                            recipients.Add(new SignalServiceAddress(t.ThreadID));
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        SignalServiceDataMessage ssdm = messageBuilder.build();
                        SignalManager.sendMessage(recipients, ssdm);
                        //TODO update database: send successfull
                        //TODO notify UI
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                        Debug.WriteLine(e.StackTrace);
                        OutgoingQueue.Add(t);
                        //TODO notify UI
                    }
                }
            }
            Debug.WriteLine("HandleOutgoingMessages finished");
            OutgoingOffSwitch.Set();
        }
    }
}