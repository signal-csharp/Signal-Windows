using GalaSoft.MvvmLight;
using Google.Protobuf;
using libsignal_service_dotnet.messages.calls;
using libsignalservice;
using libsignalservice.messages;
using Microsoft.Extensions.Logging;
using Org.WebRtc;
using Signal_Windows.SignalWebRtc;
using Signal_Windows.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace Signal_Windows.ViewModels
{
    public class CallPageViewModel : ViewModelBase
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<CallPageViewModel>();
        private ulong Id;
        private string ConversationId;
        private RTCPeerConnection PeerConnection;
        private RTCDataChannel DataChannel;
        private string Description;
        private CancellationTokenSource CancelSource = new CancellationTokenSource();

        string _CallerDisplayName;
        public string CallerDisplayName
        {
            get { return _CallerDisplayName; }
            set { _CallerDisplayName = value; RaisePropertyChanged(nameof(CallerDisplayName));}
        }

        public CallPage View { get; internal set; }

        internal async Task HandleCallOfferMessage(SignalServiceEnvelope envelope, OfferMessage offerMessage)
        {
            //await WebRTC.RequestAccessForMediaCapture();
            ConversationId = envelope.GetSource();
            CallerDisplayName = ConversationId;
            Id = offerMessage.Id;
            Description = offerMessage.Description;
        }

        internal async Task Accept(CoreDispatcher dispatcher)
        {
            CancelSource.Cancel();
            CancelSource = new CancellationTokenSource();
            SignalServiceAccountManager accountManager = App.Handle.CreateAccountManager();
            var turnServers = await accountManager.GetTurnServerInfo(CancelSource.Token);
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
            await Task.Run(() => WebRTC.Initialize(dispatcher));
            var media = Media.CreateMedia();
            RTCMediaStreamConstraints mediaStreamConstraints = new RTCMediaStreamConstraints()
            {
                audioEnabled = true
            };

            
            PeerConnection = new RTCPeerConnection(config);
            PeerConnection.AddStream(await media.GetUserMedia(mediaStreamConstraints));
            PeerConnection.OnAddStream += PeerConnection_OnAddStream;
            PeerConnection.OnIceCandidate += PeerConnection_OnIceCandidate;
            var offer = new RTCSessionDescription(RTCSdpType.Offer, Description);
            await PeerConnection.SetRemoteDescription(offer);
            var answer = await PeerConnection.CreateAnswer();
            await PeerConnection.SetLocalDescription(answer);
            PeerConnection.OnDataChannel += PeerConnection_OnDataChannel;
            await Task.Run(() => App.Handle.SendCallResponse(ConversationId, Id, answer.Sdp));
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

        internal async Task HandleCallIceUpdatesMessage(SignalServiceEnvelope envelope, List<IceUpdateMessage> iceUpdateMessages)
        {
            if (envelope.GetSource() == ConversationId)
            {
                foreach (var update in iceUpdateMessages)
                {
                    await PeerConnection.AddIceCandidate(new RTCIceCandidate(update.Sdp, update.SdpMid, (ushort)update.SdpMLineIndex));
                }
            }
        }

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
                App.Handle.SendCallMessage(ConversationId, msg);
            });
        }

        private void PeerConnection_OnAddStream(MediaStreamEvent __param0)
        {
            Logger.LogTrace("PeerConnection_OnAddStream");
        }
    }
}
