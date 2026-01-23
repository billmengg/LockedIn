export const createPeerConnection = ({ onTrack, onIceCandidate }) => {
  const pc = new RTCPeerConnection({
    iceServers: [
      { urls: 'stun:stun.l.google.com:19302' },
      { urls: 'stun:stun1.l.google.com:19302' },
    ],
  });

  pc.ontrack = (event) => {
    if (onTrack) {
      onTrack(event);
    }
  };

  pc.onicecandidate = (event) => {
    if (event.candidate && onIceCandidate) {
      onIceCandidate(event.candidate);
    }
  };

  return pc;
};

export const safeClosePeerConnection = (pc) => {
  if (!pc) return;
  try {
    pc.ontrack = null;
    pc.onicecandidate = null;
    pc.close();
  } catch (err) {
    // Best-effort cleanup
    // eslint-disable-next-line no-console
    console.error('Failed to close peer connection', err);
  }
};
