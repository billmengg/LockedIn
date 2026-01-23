import React, { useState, useEffect, useRef } from 'react';
import './Dashboard.css';
import { getDevices, pairDevice } from '../utils/api';
import { getToken } from '../utils/auth';
import { io } from 'socket.io-client';
import VideoStream from './VideoStream';
import { createPeerConnection, safeClosePeerConnection } from '../utils/webrtc';
import { loadPairedDevices, savePairedDevices, upsertPairedDevice, updateDevicePairingCode, updateDeviceName } from '../utils/deviceStorage';

const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000';

function Dashboard({ onLogout }) {
  const [devices, setDevices] = useState([]);
  const [pairingCode, setPairingCode] = useState('');
  const [showPairing, setShowPairing] = useState(false);
  const [loading, setLoading] = useState(false);
  const [streaming, setStreaming] = useState(false);
  const [remoteStream, setRemoteStream] = useState(null);
  const [streamError, setStreamError] = useState('');
  const [remoteFrame, setRemoteFrame] = useState('');
  const [socketStatus, setSocketStatus] = useState('disconnected');
  const [reconnectTarget, setReconnectTarget] = useState(null);
  const [pairedConfirmed, setPairedConfirmed] = useState(false);
  const [selectedDeviceId, setSelectedDeviceId] = useState(null);
  const socketRef = useRef(null);
  const pcRef = useRef(null);

  useEffect(() => {
    const cachedDevices = loadPairedDevices();
    if (cachedDevices.length > 0) {
      setDevices(cachedDevices);
    }
    loadDevices();
    const socket = connectSocket();

    return () => {
      if (socket) {
        socket.disconnect();
      }
      safeClosePeerConnection(pcRef.current);
      pcRef.current = null;
    };
  }, []);

  const loadDevices = async () => {
    try {
      const data = await getDevices();
      const freshDevices = data.devices || [];
      if (freshDevices.length > 0) {
        setDevices(freshDevices);
        savePairedDevices(freshDevices);
        setPairedConfirmed(true);
      } else {
        setPairedConfirmed(false);
      }
    } catch (err) {
      console.error('Failed to load devices:', err);
      setPairedConfirmed(false);
    }
  };

  const connectSocket = () => {
    const token = getToken();
    const socket = io(API_URL, {
      auth: { token }
    });

    socket.on('connect', () => {
      // eslint-disable-next-line no-console
      console.log('[socket] connected', socket.id);
      setSocketStatus('connected');
      socket.emit('register-parent', { token });
    });

    socket.on('connect_error', (err) => {
      // eslint-disable-next-line no-console
      console.error('[socket] connect_error', err.message || err);
      setSocketStatus('error');
    });

    socket.on('disconnect', (reason) => {
      // eslint-disable-next-line no-console
      console.log('[socket] disconnected', reason);
      setSocketStatus('disconnected');
    });

    socket.on('device-status', (data) => {
      const updated = upsertPairedDevice({
        id: data.deviceId,
        online: data.online
      });
      setDevices(updated);
    });

    socket.on('stream-requested', (data) => {
      // eslint-disable-next-line no-console
      console.log('[stream] stream-requested', data);
    });

    socket.on('error', (data) => {
      // eslint-disable-next-line no-console
      console.error('[socket] error', data);
      if (data?.message) {
        setStreamError(data.message);
        if (data.message === 'No paired device') {
          setPairedConfirmed(false);
          setShowPairing(true);
        }
      }
    });

    socket.on('webrtc-answer', async (data) => {
      try {
        if (!pcRef.current) return;
        const { answer } = data;
        await pcRef.current.setRemoteDescription(answer);
      } catch (err) {
        setStreamError('Failed to apply WebRTC answer');
        // eslint-disable-next-line no-console
        console.error(err);
      }
    });

    socket.on('webrtc-ice', async (data) => {
      try {
        if (!pcRef.current) return;
        const { candidate } = data;
        if (candidate) {
          await pcRef.current.addIceCandidate(candidate);
        }
      } catch (err) {
        setStreamError('Failed to add ICE candidate');
        // eslint-disable-next-line no-console
        console.error(err);
      }
    });

    socket.on('frame', (data) => {
      if (data?.dataUrl) {
        setRemoteFrame(data.dataUrl);
      }
    });

    socketRef.current = socket;
    return socket;
  };

  const handlePair = async () => {
    if (!pairingCode || pairingCode.length !== 6) {
      alert('Please enter a valid 6-digit pairing code');
      return;
    }

    setLoading(true);
    try {
      const result = await pairDevice(pairingCode);
      await loadDevices();
      if (result?.deviceId) {
        updateDevicePairingCode(result.deviceId, pairingCode);
      }
      if (devices[0]) {
        upsertPairedDevice(devices[0]);
      }
      setShowPairing(false);
      setPairingCode('');
      setReconnectTarget(null);
      setPairedConfirmed(true);
    } catch (err) {
      alert(err.response?.data?.error || 'Pairing failed');
    } finally {
      setLoading(false);
    }
  };

  const handleReconnect = async (device) => {
    if (!device) return;
    if (device.pairingCode) {
      setLoading(true);
      try {
        await pairDevice(device.pairingCode);
        await loadDevices();
        setPairedConfirmed(true);
      } catch (err) {
        alert(err.response?.data?.error || 'Reconnect failed');
      } finally {
        setLoading(false);
      }
    } else {
      setReconnectTarget(device);
      setShowPairing(true);
    }
  };

  const handleViewScreen = async () => {
    if (streaming) return;
    setStreamError('');

    const token = getToken();
    const socket = socketRef.current;
    if (!socket || !socket.connected) {
      setStreamError('Socket not connected. Try refreshing the page.');
      // eslint-disable-next-line no-console
      console.error('[stream] socket not connected', socketStatus);
      return;
    }

    if (!pairedConfirmed) {
      setStreamError('No paired device. Pair or reconnect first.');
      setShowPairing(true);
      return;
    }

    try {
      // eslint-disable-next-line no-console
      console.log('[stream] requesting stream');
      const pc = createPeerConnection({
        onTrack: (event) => {
          const [stream] = event.streams;
          if (stream) {
            setRemoteStream(stream);
          }
        },
        onIceCandidate: (candidate) => {
          socket.emit('webrtc-ice', { token, candidate });
        },
      });

      pc.ondatachannel = (event) => {
        const channel = event.channel;
        channel.onmessage = (msgEvent) => {
          if (typeof msgEvent.data === 'string' && msgEvent.data.startsWith('data:image')) {
            setRemoteFrame(msgEvent.data);
          }
        };
      };

      pcRef.current = pc;
      setStreaming(true);

      const offer = await pc.createOffer({
        offerToReceiveVideo: true,
        offerToReceiveAudio: false,
      });

      await pc.setLocalDescription(offer);

      socket.emit('webrtc-offer', { token, offer });
      socket.emit('request-stream', { token });
      // eslint-disable-next-line no-console
      console.log('[stream] request-stream emitted');
    } catch (err) {
      setStreamError('Failed to start stream');
      // eslint-disable-next-line no-console
      console.error(err);
      safeClosePeerConnection(pcRef.current);
      pcRef.current = null;
      setStreaming(false);
    }
  };

  const handleStopStream = () => {
    const token = getToken();
    const socket = socketRef.current;
    if (socket && socket.connected) {
      socket.emit('stop-stream', { token });
    }
    safeClosePeerConnection(pcRef.current);
    pcRef.current = null;
    setRemoteStream(null);
    setRemoteFrame('');
    setStreaming(false);
  };

  const handleRename = (deviceId, name) => {
    const updated = updateDeviceName(deviceId, name);
    setDevices(updated);
  };

  const handleSelectDevice = (deviceId) => {
    setSelectedDeviceId(deviceId);
    setStreamError('');
  };

  const handleBackToList = () => {
    setSelectedDeviceId(null);
    setRemoteStream(null);
    setRemoteFrame('');
    setStreaming(false);
    safeClosePeerConnection(pcRef.current);
    pcRef.current = null;
  };

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <h1>Parent Dashboard</h1>
        <button onClick={onLogout} className="logout-button">Logout</button>
      </header>

      <div className="dashboard-content">
        {devices.length > 0 ? (
          <div className="device-list">
            <div className="device-list-header">
              <h2>Your Devices</h2>
              <button 
                onClick={() => setShowPairing(true)} 
                className="primary-button"
              >
                Add Device
              </button>
            </div>
            {!selectedDeviceId && devices.map((device) => (
              <div key={device.id} className="device-card">
                <div className="device-info">
                  <p><strong>Status:</strong> 
                    <span className={device.online ? 'online' : 'offline'}>
                      {device.online ? ' Online' : ' Offline'}
                    </span>
                  </p>
                  <p><strong>Device ID:</strong> {device.id}</p>
                  <div className="device-name">
                    <label>Name</label>
                    <input
                      type="text"
                      value={device.name || ''}
                      placeholder="Name this device"
                      onChange={(e) => handleRename(device.id, e.target.value)}
                    />
                  </div>
                </div>
                <div className="stream-controls">
                  <button
                    onClick={() => handleSelectDevice(device.id)}
                    className="primary-button"
                  >
                    Open
                  </button>
                </div>
              </div>
            ))}
            {selectedDeviceId && (
              <div className="device-card">
                <div className="device-list-header">
                  <h2>Device</h2>
                  <button onClick={handleBackToList} className="secondary-button">
                    Back
                  </button>
                </div>
                {devices.filter((d) => d.id === selectedDeviceId).map((device) => (
                  <div key={device.id}>
                    <div className="device-info">
                      <p><strong>Status:</strong> 
                        <span className={device.online ? 'online' : 'offline'}>
                          {device.online ? ' Online' : ' Offline'}
                        </span>
                      </p>
                      <p><strong>Device ID:</strong> {device.id}</p>
                      <div className="device-name">
                        <label>Name</label>
                        <input
                          type="text"
                          value={device.name || ''}
                          placeholder="Name this device"
                          onChange={(e) => handleRename(device.id, e.target.value)}
                        />
                      </div>
                    </div>

                    {device.online && pairedConfirmed && (
                      <div className="stream-controls">
                        <button 
                          onClick={handleViewScreen} 
                          className="view-button"
                          disabled={streaming}
                        >
                          {streaming ? 'Streaming...' : 'View Screen'}
                        </button>
                        {streaming && (
                          <button 
                            onClick={handleStopStream} 
                            className="secondary-button"
                          >
                            Stop
                          </button>
                        )}
                        <span className="socket-status">Socket: {socketStatus}</span>
                      </div>
                    )}
                    {!pairedConfirmed && (
                      <div className="pairing-required">
                        Pair or reconnect this device before streaming.
                        <button
                          onClick={() => setShowPairing(true)}
                          className="primary-button"
                          disabled={loading}
                        >
                          Pair / Reconnect
                        </button>
                      </div>
                    )}
                    {!device.online && (
                      <div className="stream-controls">
                        <button
                          onClick={() => handleReconnect(device)}
                          className="primary-button"
                          disabled={loading}
                        >
                          {loading ? 'Reconnecting...' : 'Reconnect'}
                        </button>
                      </div>
                    )}
                    {streamError && (
                      <div className="error">{streamError}</div>
                    )}
                    {device.online && (
                      <VideoStream stream={remoteStream} frameDataUrl={remoteFrame} />
                    )}
                  </div>
                ))}
              </div>
            )}
            {showPairing && (
              <div className="pairing-form">
                {reconnectTarget && (
                  <div className="reconnect-note">
                    Reconnect to device: {reconnectTarget.id}
                  </div>
                )}
                <input
                  type="text"
                  placeholder="Enter 6-digit code"
                  value={pairingCode}
                  onChange={(e) => setPairingCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                  maxLength={6}
                />
                <div className="button-group">
                  <button onClick={handlePair} disabled={loading} className="primary-button">
                    {loading ? 'Pairing...' : 'Pair Device'}
                  </button>
                  <button onClick={() => setShowPairing(false)} className="secondary-button">
                    Cancel
                  </button>
                </div>
              </div>
            )}
          </div>
        ) : (
          <div className="device-card">
            <h2>No Device Paired</h2>
            <p>Pair a device to get started.</p>
            
            {showPairing ? (
              <div className="pairing-form">
                {reconnectTarget && (
                  <div className="reconnect-note">
                    Reconnect to device: {reconnectTarget.id}
                  </div>
                )}
                <input
                  type="text"
                  placeholder="Enter 6-digit code"
                  value={pairingCode}
                  onChange={(e) => setPairingCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                  maxLength={6}
                />
                <div className="button-group">
                  <button onClick={handlePair} disabled={loading} className="primary-button">
                    {loading ? 'Pairing...' : 'Pair Device'}
                  </button>
                  <button onClick={() => setShowPairing(false)} className="secondary-button">
                    Cancel
                  </button>
                </div>
              </div>
            ) : (
              <button 
                onClick={() => setShowPairing(true)} 
                className="primary-button"
              >
                Pair Device
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

export default Dashboard;

