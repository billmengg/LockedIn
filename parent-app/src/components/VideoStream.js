import React, { useEffect, useRef, useState } from 'react';
import './VideoStream.css';

function VideoStream({ stream, frameDataUrl }) {
  const videoRef = useRef(null);
  const resizingRef = useRef(null);
  const [frameWidth, setFrameWidth] = useState(720);

  useEffect(() => {
    if (videoRef.current) {
      videoRef.current.srcObject = stream || null;
    }
  }, [stream]);

  useEffect(() => {
    const onMouseMove = (event) => {
      if (!resizingRef.current) return;
      const { startX, startWidth, side } = resizingRef.current;
      const delta = side === 'right'
        ? event.clientX - startX
        : startX - event.clientX;
      const maxWidth = Math.max(480, window.innerWidth - 80);
      const nextWidth = Math.max(320, Math.min(maxWidth, startWidth + delta));
      setFrameWidth(nextWidth);
    };

    const onMouseUp = () => {
      resizingRef.current = null;
    };

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
    return () => {
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };
  }, []);

  const startResize = (side) => (event) => {
    event.preventDefault();
    resizingRef.current = {
      side,
      startX: event.clientX,
      startWidth: frameWidth,
    };
  };

  const frameHeight = Math.round((frameWidth * 9) / 16);

  return (
    <div className="video-stream-wrapper">
      <div
        className="resizable-frame"
        style={{ width: `${frameWidth}px`, height: `${frameHeight}px` }}
      >
        <div className="video-stream">
          {stream ? (
            <video ref={videoRef} autoPlay playsInline muted />
          ) : frameDataUrl ? (
            <img className="frame-image" src={frameDataUrl} alt="Live stream" />
          ) : (
            <div className="video-placeholder">Waiting for stream...</div>
          )}
        </div>
        <div
          className="resize-handle resize-handle-left"
          onMouseDown={startResize('left')}
        />
        <div
          className="resize-handle resize-handle-right"
          onMouseDown={startResize('right')}
        />
      </div>
    </div>
  );
}

export default VideoStream;
