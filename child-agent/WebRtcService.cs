using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SIPSorcery.Net;

namespace AccountabilityAgent
{
    public class WebRtcService : IDisposable
    {
        private readonly RTCPeerConnection _peerConnection;
        private RTCDataChannel? _dataChannel;
        private bool _dataChannelOpen = false;

        public event Action<RTCIceCandidateInit>? IceCandidateDiscovered;

        public WebRtcService()
        {
            var config = new RTCConfiguration
            {
                iceServers = new List<RTCIceServer>
                {
                    new RTCIceServer { urls = "stun:stun.l.google.com:19302" },
                    new RTCIceServer { urls = "stun:stun1.l.google.com:19302" }
                }
            };

            _peerConnection = new RTCPeerConnection(config);

            // Data channel for frame streaming (JPEG base64). If SCTP is unavailable,
            // fall back to Socket.IO frame relay instead of crashing.
            try
            {
                _dataChannel = _peerConnection.createDataChannel("screen", new RTCDataChannelInit()).GetAwaiter().GetResult();
                _dataChannel.onopen += () => { _dataChannelOpen = true; };
                _dataChannel.onclose += () => { _dataChannelOpen = false; };
            }
            catch
            {
                _dataChannel = null;
                _dataChannelOpen = false;
            }

            _peerConnection.onicecandidate += (candidate) =>
            {
                if (candidate != null)
                {
                    var init = new RTCIceCandidateInit
                    {
                        candidate = candidate.candidate,
                        sdpMid = candidate.sdpMid,
                        sdpMLineIndex = candidate.sdpMLineIndex
                    };
                    IceCandidateDiscovered?.Invoke(init);
                }
            };
        }

        public Task<RTCSessionDescriptionInit> HandleOfferAsync(string sdp, string type)
        {
            var offer = new RTCSessionDescriptionInit
            {
                sdp = sdp,
                type = RTCSdpType.offer
            };

            var setRemoteResult = _peerConnection.setRemoteDescription(offer);
            if (setRemoteResult != SetDescriptionResultEnum.OK)
            {
                throw new InvalidOperationException($"Failed to set remote description: {setRemoteResult}");
            }

            var answer = _peerConnection.createAnswer(null);
            _peerConnection.setLocalDescription(answer);

            return Task.FromResult(answer);
        }

        public void AddIceCandidate(RTCIceCandidateInit candidate)
        {
            _peerConnection.addIceCandidate(candidate);
        }

        public bool SendFrame(string dataUrl)
        {
            if (!_dataChannelOpen) return false;
            if (string.IsNullOrEmpty(dataUrl)) return false;
            if (_dataChannel == null) return false;
            _dataChannel.send(dataUrl);
            return true;
        }

        public void Dispose()
        {
            try
            {
                _dataChannel?.close();
            }
            catch
            {
                // Ignore close errors
            }
            _peerConnection?.close();
        }
    }
}
