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
        private BlockingCollection<SignalMessage> OutgoingQueue = new BlockingCollection<SignalMessage>(new ConcurrentQueue<SignalMessage>());

        private ManualResetEvent OutgoingMessagesSavedEvent = new ManualResetEvent(false);

        /// <summary>
        /// Reads pending messages from the <see cref="OutgoingQueue"/> and attempts to send them
        /// </summary>
        public void HandleOutgoingMessages()
        {
            Debug.WriteLine("HandleOutgoingMessages starting...");
            CancellationToken token = CancelSource.Token;
            WaitHandle[] handles = { OutgoingMessagesSavedEvent, token.WaitHandle };
            while (!token.IsCancellationRequested)
            {
                SignalMessage t = null;
                try
                {
                    t = OutgoingQueue.Take(token);
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
                    WaitHandle.WaitAny(handles);
                    if (!token.IsCancellationRequested)
                    {
                        OutgoingMessagesSavedEvent.Reset();
                        SignalManager.sendMessage(recipients, ssdm);
                        //TODO update database: send successful
                        //TODO notify UI
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
                    OutgoingQueue.Add(t);
                    //TODO notify UI
                }
            }
            Debug.WriteLine("HandleOutgoingMessages finished");
            OutgoingOffSwitch.Set();
        }
    }
}