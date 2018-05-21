using Google.Protobuf;
using libsignal_service_dotnet.messages.calls;
using libsignalservice;
using Microsoft.Extensions.Logging;
using Org.WebRtc;
using Signal_Windows.Lib;
using Signal_Windows.Models;
using Signal_Windows.ViewModels;
using Signal_Windows.Views;
using Signal_Windows.SignalWebRtc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using libsignalservice.messages;

namespace Signal_Windows
{
    public class SignalWindowsFrontend : ISignalFrontend
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<SignalWindowsFrontend>();
        public CoreDispatcher Dispatcher { get; }
        public ViewModelLocator Locator { get; }
        public int ViewId { get; }
        public SignalWindowsFrontend(CoreDispatcher dispatcher, ViewModelLocator locator, int viewId)
        {
            Dispatcher = dispatcher;
            Locator = locator;
            ViewId = viewId;
        }
        public void AddOrUpdateConversation(SignalConversation conversation, SignalMessage updateMessage)
        {
            Locator.MainPageInstance.AddOrUpdateConversation(conversation, updateMessage);
        }

        public void HandleIdentitykeyChange(LinkedList<SignalMessage> messages)
        {
            Locator.MainPageInstance.HandleIdentitykeyChange(messages);
        }

        public AppendResult HandleMessage(SignalMessage message, SignalConversation conversation)
        {
            Logger.LogInformation("SignalWindowsFrontend {0} handling message {1}", ViewId, message.ComposedTimestamp);
            var result = Locator.MainPageInstance.HandleMessage(message, conversation);
            CheckNotification(conversation);
            return result;
        }

        public void HandleMessageUpdate(SignalMessage updatedMessage)
        {
            Locator.MainPageInstance.HandleMessageUpdate(updatedMessage);
        }

        public void ReplaceConversationList(List<SignalConversation> conversations)
        {
            Locator.MainPageInstance.ReplaceConversationList(conversations);
        }

        public void HandleAuthFailure()
        {
            Logger.LogInformation("HandleAuthFailure() {0}", ViewId);
            if (ViewId == App.MainViewId)
            {
                Frame f = (Frame)Window.Current.Content;
                f.Navigate(typeof(StartPage));
                CoreApplication.GetCurrentView().CoreWindow.Activate();
                ApplicationViewSwitcher.TryShowAsStandaloneAsync(App.MainViewId);
            }
            else
            {
                CoreApplication.GetCurrentView().CoreWindow.Close();
            }
        }

        public void HandleAttachmentStatusChanged(SignalAttachment sa)
        {
            Locator.MainPageInstance.HandleAttachmentStatusChanged(sa);
        }

        public void HandleMessageRead(long unreadMarkerIndex, SignalConversation conversation)
        {
            Locator.MainPageInstance.HandleMessageRead(unreadMarkerIndex, conversation);
            CheckNotification(conversation);
        }

        public void HandleUnreadMessage(SignalMessage message)
        {
            if (ApplicationView.GetForCurrentView().Id == App.MainViewId)
            {
                NotificationsUtils.Notify(message);
            }
        }

        private void CheckNotification(SignalConversation conversation)
        {
            if (ApplicationView.GetForCurrentView().Id == App.MainViewId)
            {
                if (conversation.UnreadCount == 0)
                {
                    NotificationsUtils.Withdraw(conversation.ThreadId);
                }
            }
        }

        ///*
        public async Task HandleCallIceUpdatesMessage(SignalServiceEnvelope envelope, List<IceUpdateMessage> iceUpdateMessages)
        {
            await Locator.CallPageInstance.HandleCallIceUpdatesMessage(envelope, iceUpdateMessages);
        }

        public async Task HandleCallOfferMessage(SignalServiceEnvelope envelope, OfferMessage offerMessage)
        {
            Locator.MainPageInstance.View.Frame.Navigate(typeof(CallPage));
            await Locator.CallPageInstance.HandleCallOfferMessage(envelope, offerMessage);
        }
        //*/
        /*
        public async Task HandleCallIceUpdatesMessage(SignalServiceEnvelope envelope, List<IceUpdateMessage> iceUpdateMessages)
        {
            foreach (var update in iceUpdateMessages)
            {
                await PeerConnection.AddIceCandidate(new RTCIceCandidate(update.Sdp, update.SdpMid, (ushort)update.SdpMLineIndex));
            }
        }

        public async Task HandleCallOfferMessage(SignalServiceEnvelope envelope, OfferMessage offerMessage)
        {
            Task.Run(async () =>
            {
                //await WebRTC.RequestAccessForMediaCapture();
                CancellationTokenSource cancelSource = new CancellationTokenSource();
                SignalServiceAccountManager accountManager = App.Handle.CreateAccountManager();
                var turnServers = await accountManager.GetTurnServerInfo(cancelSource.Token);
                List<RTCIceServer> rtcServers = new List<RTCIceServer>();
                foreach (var server in turnServers.Urls)
                {
                    var rtcServer = new RTCIceServer()
                    {
                        Credential = turnServers.Password,
                        Username = turnServers.Username,
                        Url = server
                    };
                    rtcServers.Add(rtcServer);
                }
                var config = new RTCConfiguration() { IceServers = rtcServers };
                WebRTC.Initialize(this.Dispatcher);
                var media = Media.CreateMedia();
                RTCMediaStreamConstraints mediaStreamConstraints = new RTCMediaStreamConstraints()
                {
                    audioEnabled = true
                };
                Id = offerMessage.Id;
                PeerConnection = new RTCPeerConnection(config);
                PeerConnection.AddStream(await media.GetUserMedia(mediaStreamConstraints));
                PeerConnection.OnAddStream += PeerConnection_OnAddStream;
                PeerConnection.OnIceCandidate += PeerConnection_OnIceCandidate;
                var offer = new RTCSessionDescription(RTCSdpType.Offer, offerMessage.Description);
                await PeerConnection.SetRemoteDescription(offer);
                var answer = await PeerConnection.CreateAnswer();
                await PeerConnection.SetLocalDescription(answer);
                PeerConnection.OnDataChannel += PeerConnection_OnDataChannel;
                App.Handle.SendCallResponse("+4917643847462", Id, answer.Sdp);
            });
        }

        private void PeerConnection_OnDataChannel(RTCDataChannelEvent __param0)
        {
            DataChannel = __param0.Channel;
            DataChannel.Send(new BinaryDataChannelMessage(new Data()
            {
                Connected = new Connected()
                {
                    Id = Id
                }
            }.ToByteArray()));
        }

        private static string sdp;
        private ulong Id;
        private RTCPeerConnection PeerConnection;
        private RTCDataChannel DataChannel;

        private void PeerConnection_OnIceCandidate(RTCPeerConnectionIceEvent __param0)
        {
            Task.Run(() =>
            {
                Logger.LogTrace("PeerConnection_OnIceCandidate");
                RTCIceCandidate candidate = __param0.Candidate;
                var msg = new SignalServiceCallMessage()
                {
                    IceUpdateMessages = new List<IceUpdateMessage>()
                {
                    new IceUpdateMessage()
                    {
                        Id = Id,
                        SdpMid = candidate.SdpMid,
                        Sdp = candidate.Candidate,
                        SdpMLineIndex = candidate.SdpMLineIndex
                    }
                }
                };
                App.Handle.SendCallMessage("+4917643847462", msg);
            });
        }

        private void PeerConnection_OnAddStream(MediaStreamEvent __param0)
        {
            Logger.LogTrace("PeerConnection_OnAddStream");
        }
*/
    }
}
