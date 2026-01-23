import React, { useEffect, useRef } from 'react';
import './VideoStream.css';

function VideoStream({ stream, frameDataUrl }) {
  const videoRef = useRef(null);

  useEffect(() => {
    if (videoRef.current) {
      videoRef.current.srcObject = stream || null;
    }
  }, [stream]);

  return (
    <div className="video-stream">
      {stream ? (
        <video ref={videoRef} autoPlay playsInline muted />
      ) : frameDataUrl ? (
        <img className="frame-image" src={frameDataUrl} alt="Live stream" />
      ) : (
        <div className="video-placeholder">Waiting for stream...</div>
      )}
    </div>
  );
}

export default VideoStream;
