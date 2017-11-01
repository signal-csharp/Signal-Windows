using libsignal;
using libsignalservice.crypto;
using libsignalservice.messages;
using libsignalservice.push;
using libsignalservice.push.exceptions;
using libsignalservice.util;
using Microsoft.Extensions.Logging;
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
        public BlockingCollection<SignalMessage> OutgoingQueue = new BlockingCollection<SignalMessage>(new ConcurrentQueue<SignalMessage>());

        /// <summary>
        /// Reads pending messages from the <see cref="OutgoingQueue"/> and attempts to send them
        /// </summary>
        public void HandleOutgoingMessages()
        {
            Logger.LogDebug("HandleOutgoingMessages()");
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

                    if (!outgoingSignalMessage.ThreadId.EndsWith("="))
                    {
                        if (!token.IsCancellationRequested)
                        {
                            MessageSender.sendMessage(new SignalServiceAddress(outgoingSignalMessage.ThreadId), message);
                            outgoingSignalMessage.Status = SignalMessageStatus.Confirmed;
                        }
                    }
                    else
                    {
                        List<SignalServiceAddress> recipients = new List<SignalServiceAddress>();
                        SignalGroup g = SignalDBContext.GetOrCreateGroupLocked(outgoingSignalMessage.ThreadId, 0, this);
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
                            outgoingSignalMessage.Status = SignalMessageStatus.Confirmed;
                        }
                    }
                }
                catch (OperationCanceledException e)
                {
                    Logger.LogInformation("HandleOutgoingMessages() finished");
                    return;
                }
                catch (EncapsulatedExceptions exceptions)
                {
                    outgoingSignalMessage.Status = SignalMessageStatus.Confirmed;
                    Logger.LogError("HandleOutgoingMessages() encountered libsignal exceptions");
                    IList<UntrustedIdentityException> identityExceptions = exceptions.getUntrustedIdentityExceptions();
                    if (exceptions.getNetworkExceptions().Count > 0)
                    {
                        outgoingSignalMessage.Status = SignalMessageStatus.Failed_Network;
                    }
                    if (identityExceptions.Count > 0)
                    {
                        outgoingSignalMessage.Status = SignalMessageStatus.Failed_Identity;
                    }
                    foreach (UntrustedIdentityException e in identityExceptions)
                    {
                        Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                        {
                            await MainPage.NotifyNewIdentity(e.getE164Number());
                        }).AsTask().Wait();
                        LibsignalDBContext.SaveIdentityLocked(new SignalProtocolAddress(e.getE164Number(), 1), Base64.encodeBytes(e.getIdentityKey().serialize()));
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
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        await MainPage.NotifyNewIdentity(e.getE164Number());
                    }).AsTask().Wait();
                    LibsignalDBContext.SaveIdentityLocked(new SignalProtocolAddress(e.getE164Number(), 1), Base64.encodeBytes(e.getIdentityKey().serialize()));
                }
                catch (Exception e)
                {
                    var line = new StackTrace(e, true).GetFrames()[0].GetFileLineNumber();
                    Logger.LogError("HandleOutgoingMessages() failed in line {0}: {1}\n{2}", line, e.Message, e.StackTrace);
                    outgoingSignalMessage.Status = SignalMessageStatus.Failed_Unknown;
                }
                SignalDBContext.UpdateMessageStatus(outgoingSignalMessage, this);
            }
            Logger.LogInformation("HandleOutgoingMessages() finished");
        }
    }
}