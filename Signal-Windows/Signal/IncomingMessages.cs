using libsignalservice.crypto;
using libsignalservice.messages;
using libsignalservice.push;
using libsignalservice.util;
using Nito.AsyncEx;
using Signal_Windows.Models;
using Signal_Windows.Storage;
using Strilanc.Value;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;
using Microsoft.QueryStringDotNET;

namespace Signal_Windows.ViewModels
{
    public partial class MainPageViewModel
    {
        /// <summary>
        /// Reads, decrypts, handles and schedules storing and displaying of incoming messages from the pipe
        /// </summary>
        public void HandleIncomingMessages()
        {
            Debug.WriteLine("HandleIncomingMessages starting...");
            try
            {
                while (Running)
                {
                    try
                    {
                        Pipe.ReadBlocking(this);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                        Debug.WriteLine(e.StackTrace);
                    }
                }
            }
            catch (Exception) { }
            Debug.WriteLine("HandleIncomingMessages finished");
        }

        /// <summary>
        /// onMessages is called from the pipe after it received messages
        /// </summary>
        /// <param name="envelopes"></param>
        public void onMessages(SignalServiceEnvelope[] envelopes)
        {
            List<SignalMessage> messages = new List<SignalMessage>();
            foreach (var envelope in envelopes)
            {
                if (envelope == null)
                {
                    continue;
                }
                try
                {
                    if (envelope.isReceipt())
                    {
                        SignalDBContext.IncreaseReceiptCountLocked(envelope, this);
                    }
                    else if (envelope.isPreKeySignalMessage() || envelope.isSignalMessage())
                    {
                        HandleMessage(envelope);
                    }
                    else
                    {
                        Debug.WriteLine("received message of unknown type " + envelope.getType() + " from " + envelope.getSource());
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine(e.StackTrace);
                }
            }
        }

        private void HandleMessage(SignalServiceEnvelope envelope)
        {
            var cipher = new SignalServiceCipher(new SignalServiceAddress(App.Store.Username), new Store());
            var content = cipher.decrypt(envelope);
            long timestamp = Util.CurrentTimeMillis();

            if (content.Message != null)
            {
                SignalServiceDataMessage message = content.Message;
                if (message.EndSession)
                {
                    HandleSessionResetMessage(envelope, content, message, false, timestamp);
                }
                else if (message.IsGroupUpdate())
                {
                    if (message.Group.Type == SignalServiceGroup.GroupType.UPDATE)
                    {
                        HandleGroupUpdateMessage(envelope, content, message, false, timestamp);
                    }
                }
                else if (message.ExpirationUpdate)
                {
                    HandleExpirationUpdateMessage(envelope, content, message, false, timestamp);
                }
                else
                {
                    HandleSignalMessage(envelope, content, message, false, timestamp);
                }
            }
            else if (content.SynchronizeMessage != null)
            {
                if (content.SynchronizeMessage.getSent().HasValue)
                {
                    var syncMessage = content.SynchronizeMessage.getSent().ForceGetValue();
                    var dataMessage = syncMessage.getMessage();

                    if (dataMessage.EndSession)
                    {
                        HandleSessionResetMessage(envelope, content, dataMessage, true, timestamp);
                    }
                    else if (dataMessage.IsGroupUpdate())
                    {
                        if (dataMessage.Group.Type == SignalServiceGroup.GroupType.UPDATE)
                        {
                            HandleGroupUpdateMessage(envelope, content, dataMessage, true, timestamp);
                        }
                    }
                    else if (dataMessage.ExpirationUpdate)
                    {
                        HandleExpirationUpdateMessage(envelope, content, dataMessage, true, timestamp);
                    }
                    else
                    {
                        HandleSignalMessage(envelope, content, dataMessage, true, timestamp);
                    }
                }
                else if (content.SynchronizeMessage.getRead().HasValue)
                {
                    var readMessages = content.SynchronizeMessage.getRead().ForceGetValue();
                    foreach (var readMessage in readMessages)
                    {
                        //TODO
                    }
                }
            } //TODO callmessages
            else
            {
                Debug.WriteLine("HandleMessage got unrecognized message from " + envelope.getSource());
            }
        }

        private void HandleExpirationUpdateMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage message, bool isSync, long timestamp)
        {
            SignalMessageDirection type;
            SignalContact author;
            SignalMessageStatus status;
            string prefix;
            string conversationId;
            long composedTimestamp;

            if (isSync)
            {
                var sent = content.SynchronizeMessage.getSent().ForceGetValue();
                type = SignalMessageDirection.Synced;
                status = SignalMessageStatus.Confirmed;
                composedTimestamp = sent.getTimestamp();
                author = null;
                prefix = "You have";
                if (message.Group != null)
                {
                    conversationId = Base64.encodeBytes(message.Group.GroupId);
                }
                else
                {
                    conversationId = sent.getDestination().ForceGetValue();
                }
            }
            else
            {
                status = 0;
                type = SignalMessageDirection.Incoming;
                author = SignalDBContext.GetOrCreateContactLocked(envelope.getSource(), timestamp, this);
                prefix = $"{author.ThreadDisplayName} has";
                composedTimestamp = envelope.getTimestamp();
                if (message.Group != null)
                {
                    conversationId = Base64.encodeBytes(message.Group.GroupId);
                }
                else
                {
                    conversationId = envelope.getSource();
                }
            }
            SignalDBContext.UpdateExpiresInLocked(new SignalConversation() { ThreadId = conversationId }, (uint)message.ExpiresInSeconds);
            SignalMessage sm = new SignalMessage()
            {
                Direction = type,
                Type = SignalMessageType.ExpireUpdate,
                Status = status,
                Author = author,
                Content = new SignalMessageContent() { Content = $"{prefix} set the expiration timer to {message.ExpiresInSeconds} seconds." },
                ThreadId = conversationId,
                DeviceId = (uint)envelope.getSourceDevice(),
                Receipts = 0,
                ComposedTimestamp = composedTimestamp,
                ReceivedTimestamp = timestamp,
            };
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await UIHandleIncomingMessage(sm);
            }).AsTask().Wait();
        }

        private void HandleSessionResetMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage, bool isSync, long timestamp)
        {
            SignalMessageDirection type;
            SignalContact author;
            SignalMessageStatus status;
            string prefix;
            string conversationId;
            long composedTimestamp;

            if (isSync)
            {
                var sent = content.SynchronizeMessage.getSent().ForceGetValue();
                type = SignalMessageDirection.Synced;
                status = SignalMessageStatus.Confirmed;
                composedTimestamp = sent.getTimestamp();
                author = null;
                prefix = "You have";
                conversationId = sent.getDestination().ForceGetValue();
            }
            else
            {
                status = 0;
                type = SignalMessageDirection.Incoming;
                author = SignalDBContext.GetOrCreateContactLocked(envelope.getSource(), timestamp, this);
                prefix = $"{author.ThreadDisplayName} has";
                composedTimestamp = envelope.getTimestamp();
                conversationId = envelope.getSource();
            }
            LibsignalDBContext.DeleteAllSessions(conversationId);

            SignalMessage sm = new SignalMessage()
            {
                Direction = type,
                Type = SignalMessageType.SessionReset,
                Status = status,
                Author = author,
                Content = new SignalMessageContent() { Content = $"{prefix} reset the session." },
                ThreadId = conversationId,
                DeviceId = (uint)envelope.getSourceDevice(),
                Receipts = 0,
                ComposedTimestamp = composedTimestamp,
                ReceivedTimestamp = timestamp,
            };
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await UIHandleIncomingMessage(sm);
            }).AsTask().Wait();
        }

        private void HandleGroupUpdateMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage, bool isSync, long timestamp)
        {
            if (dataMessage.Group != null) //TODO check signal droid: group messages have different types!
            {
                SignalServiceGroup group = dataMessage.Group;
                string groupid = Base64.encodeBytes(group.GroupId);
                SignalGroup g = new SignalGroup();
                string displayname = "Unknown group";
                string avatarfile = null;
                if (group.Name != null)
                {
                    displayname = group.Name;
                }
                var dbgroup = SignalDBContext.InsertOrUpdateGroupLocked(groupid, displayname, avatarfile, true, timestamp, this);
                if (group.Members != null)
                {
                    foreach (var member in group.Members)
                    {
                        SignalDBContext.InsertOrUpdateGroupMembershipLocked(dbgroup.Id, SignalDBContext.GetOrCreateContactLocked(member, 0, this).Id);
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
                    var sent = content.SynchronizeMessage.getSent().ForceGetValue();
                    type = SignalMessageDirection.Synced;
                    status = SignalMessageStatus.Confirmed;
                    composedTimestamp = sent.getTimestamp();
                    author = null;
                    prefix = "You have";
                }
                else
                {
                    status = 0;
                    type = SignalMessageDirection.Incoming;
                    author = SignalDBContext.GetOrCreateContactLocked(envelope.getSource(), timestamp, this);
                    prefix = $"{author.ThreadDisplayName} has";
                    composedTimestamp = envelope.getTimestamp();
                }

                SignalMessage sm = new SignalMessage()
                {
                    Direction = type,
                    Type = SignalMessageType.GroupUpdate,
                    Status = status,
                    Author = author,
                    Content = new SignalMessageContent() { Content = $"{prefix} updated the group." },
                    ThreadId = groupid,
                    DeviceId = (uint)envelope.getSourceDevice(),
                    Receipts = 0,
                    ComposedTimestamp = composedTimestamp,
                    ReceivedTimestamp = timestamp,
                };
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    await UIHandleIncomingMessage(sm);
                }).AsTask().Wait();
            }
            else
            {
                Debug.WriteLine("received group update without group info!");
            }
        }

        private void HandleSignalMessage(SignalServiceEnvelope envelope, SignalServiceContent content, SignalServiceDataMessage dataMessage, bool isSync, long timestamp)
        {
            SignalMessageDirection type;
            SignalContact author;
            SignalMessageStatus status;
            string threadId;
            long composedTimestamp;
            string body = dataMessage.Body != null ? dataMessage.Body : "";

            if (dataMessage.Group != null)
            {
                var rawId = dataMessage.Group.GroupId;
                threadId = Base64.encodeBytes(rawId);
                var g = SignalDBContext.GetOrCreateGroupLocked(threadId, timestamp, this);
                if (!g.CanReceive)
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
                    MessageSender.sendMessage(envelope.getSourceAddress(), requestInfoMessage);
                }
                composedTimestamp = envelope.getTimestamp();
            }
            else
            {
                if (isSync)
                {
                    var sent = content.SynchronizeMessage.getSent().ForceGetValue();
                    threadId = SignalDBContext.GetOrCreateContactLocked(sent.getDestination().ForceGetValue(), timestamp, this).ThreadId;
                    composedTimestamp = sent.getTimestamp();
                }
                else
                {
                    threadId = SignalDBContext.GetOrCreateContactLocked(envelope.getSource(), timestamp, this).ThreadId;
                    composedTimestamp = envelope.getTimestamp();
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
                author = SignalDBContext.GetOrCreateContactLocked(envelope.getSource(), timestamp, this);
            }

            List<SignalAttachment> attachments = new List<SignalAttachment>();
            SignalMessage message = new SignalMessage()
            {
                Direction = type,
                Status = status,
                Author = author,
                Content = new SignalMessageContent() { Content = body },
                ThreadId = threadId,
                DeviceId = (uint)envelope.getSourceDevice(),
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
                    var pointer = receivedAttachment.asPointer();
                    SignalAttachment sa = new SignalAttachment()
                    {
                        Message = message,
                        Status = (uint)SignalAttachmentStatus.Default,
                        SentFileName = pointer.FileName,
                        ContentType = "",
                        Key = pointer.Key,
                        Relay = pointer.Relay,
                        StorageId = pointer.Id
                    };
                    attachments.Add(sa);
                }
            }
            Debug.WriteLine("received message: " + message.Content);
            if (!App.WindowActive && type == SignalMessageDirection.Incoming)
            {
                SendTileNotification(message);
                SendMessageNotification(message);
            }
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await UIHandleIncomingMessage(message);
            }).AsTask().Wait();
        }

        private void SendMessageNotification(SignalMessage message)
        {
            // notification tags can only be 16 chars (64 after creators update)
            // https://docs.microsoft.com/en-us/uwp/api/Windows.UI.Notifications.ToastNotification#Windows_UI_Notifications_ToastNotification_Tag
            string notificationId = message.ThreadId;
            ToastBindingGeneric toastBinding = new ToastBindingGeneric()
            {
                AppLogoOverride = new ToastGenericAppLogo()
                {
                    Source = "ms-appx:///Assets/gambino.png",
                    HintCrop = ToastGenericAppLogoCrop.Circle
                }
            };

            var notificationText = GetNotificationText(message);
            foreach (var item in notificationText)
            {
                toastBinding.Children.Add(item);
            }

            ToastContent toastContent = new ToastContent()
            {
                Launch = notificationId,
                Visual = new ToastVisual()
                {
                    BindingGeneric = toastBinding
                },
                DisplayTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(message.ReceivedTimestamp)
            };

            ToastNotification toastNotification = new ToastNotification(toastContent.GetXml());
            if (message.Author.ExpiresInSeconds > 0)
            {
                toastNotification.ExpirationTime = DateTime.Now.Add(TimeSpan.FromSeconds(message.Author.ExpiresInSeconds));
            }
            toastNotification.Tag = notificationId;
            ToastNotificationManager.CreateToastNotifier().Show(toastNotification);
        }

        private void SendTileNotification(SignalMessage message)
        {
            TileBindingContentAdaptive tileBindingContent = new TileBindingContentAdaptive()
            {
                PeekImage = new TilePeekImage()
                {
                    Source = "ms-appx:///Assets/gambino.png"
                }
            };
            var notificationText = GetNotificationText(message);
            foreach (var item in notificationText)
            {
                tileBindingContent.Children.Add(item);
            }

            TileBinding tileBinding = new TileBinding()
            {
                Content = tileBindingContent
            };

            TileContent tileContent = new TileContent()
            {
                Visual = new TileVisual()
                {
                    TileMedium = tileBinding,
                    TileWide = tileBinding,
                    TileLarge = tileBinding
                }
            };

            TileNotification tileNotification = new TileNotification(tileContent.GetXml());
            if (message.Author.ExpiresInSeconds > 0)
            {
                tileNotification.ExpirationTime = DateTime.Now.Add(TimeSpan.FromSeconds(message.Author.ExpiresInSeconds));
            }
            TileUpdateManager.CreateTileUpdaterForApplication().Update(tileNotification);
        }

        private IList<AdaptiveText> GetNotificationText(SignalMessage message)
        {
            List<AdaptiveText> text = new List<AdaptiveText>();
            AdaptiveText title = new AdaptiveText()
            {
                Text = message.Author.ThreadDisplayName,
                HintMaxLines = 1
            };
            AdaptiveText messageText = new AdaptiveText()
            {
                Text = message.Content.Content,
                HintWrap = true
            };
            text.Add(title);
            text.Add(messageText);
            return text;
        }
    }
}