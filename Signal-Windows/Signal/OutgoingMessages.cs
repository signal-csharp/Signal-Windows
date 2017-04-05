using libsignalservice.messages;
using libsignalservice.push;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
                    Builder messageBuilder = SignalServiceDataMessage.newBuilder().withBody(outgoingSignalMessage.Content).withTimestamp(outgoingSignalMessage.ComposedTimestamp);
                    List<SignalServiceAddress> recipients = new List<SignalServiceAddress>();
                    if (outgoingSignalMessage.ThreadID[0] == '+')
                    {
                        recipients.Add(new SignalServiceAddress(outgoingSignalMessage.ThreadID));
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                    SignalServiceDataMessage ssdm = messageBuilder.build();
                    if (!token.IsCancellationRequested)
                    {
                        SignalManager.sendMessage(recipients, ssdm);
                        try
                        {
                            lock (SignalDBContext.DBLock)
                            {
                                using (var ctx = new SignalDBContext())
                                {
                                    using (var transaction = ctx.Database.BeginTransaction())
                                    {
                                        var m = ctx.Messages.
                                            Single(t => t.ComposedTimestamp == outgoingSignalMessage.ComposedTimestamp && t.Author == null);
                                        if (m != null)
                                        {
                                            m.Status = (uint)SignalMessageStatus.Confirmed;
                                            ctx.SaveChanges();
                                            transaction.Commit();
                                            //TODO notify UI
                                        }
                                        else
                                        {
                                            Debug.WriteLine("HandleOutgoingMessages could not find the correspoding message");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine("failed to save message to db");
                            Debug.WriteLine(e.Message);
                            Debug.WriteLine(e.StackTrace);
                        }
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