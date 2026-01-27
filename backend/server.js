const express = require('express');
const http = require('http');
const { Server } = require('socket.io');
const cors = require('cors');
const jwt = require('jsonwebtoken');
const fs = require('fs');
const path = require('path');

const app = express();
const server = http.createServer(app);
const io = new Server(server, {
  maxHttpBufferSize: 10 * 1024 * 1024, // allow larger JPEG data URLs
  cors: {
    origin: "*",
    methods: ["GET", "POST"]
  }
});

app.use(cors());
app.use(express.json());

const buildPath = path.join(__dirname, '..', 'parent-app', 'build');
const hasFrontendBuild = fs.existsSync(buildPath);

// API status
app.get('/api/status', (req, res) => {
  res.json({
    message: 'Accountability Backend API',
    status: 'running',
    version: '1.0.0'
  });
});

// In-memory storage (accounts loaded from account.txt)
const users = new Map();
const devices = new Map();
const pairings = new Map();
const JWT_SECRET = process.env.JWT_SECRET || 'change-me-in-production';

const ACCOUNT_PATH = path.join(__dirname, '..', 'account.txt');
const DATA_PATH = path.join(__dirname, '..', 'backend-data.json');

const loadAccounts = () => {
  users.clear();
  try {
    const raw = fs.readFileSync(ACCOUNT_PATH, 'utf-8');
    raw.split('\n').forEach((line) => {
      const trimmed = line.trim();
      if (!trimmed || trimmed.startsWith('#')) return;
      const [usernameOrEmail, password] = trimmed.split(':');
      if (!usernameOrEmail || !password) return;
      users.set(usernameOrEmail.trim(), {
        usernameOrEmail: usernameOrEmail.trim(),
        password: password.trim(),
      });
    });
    console.log(`[accounts] loaded ${users.size} account(s) from account.txt`);
  } catch (err) {
    console.log('[accounts] account.txt not found or unreadable');
  }
};

loadAccounts();

const saveData = () => {
  try {
    const data = {
      devices: Array.from(devices.entries()),
      pairings: Array.from(pairings.entries()),
    };
    fs.writeFileSync(DATA_PATH, JSON.stringify(data, null, 2), 'utf-8');
  } catch (err) {
    console.log('[data] failed to save backend-data.json');
  }
};

const loadData = () => {
  try {
    if (!fs.existsSync(DATA_PATH)) return;
    const raw = fs.readFileSync(DATA_PATH, 'utf-8');
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed.devices)) {
      devices.clear();
      parsed.devices.forEach(([id, device]) => {
        devices.set(id, { ...device, online: false, socketId: null });
      });
    }
    if (Array.isArray(parsed.pairings)) {
      pairings.clear();
      parsed.pairings.forEach(([email, deviceId]) => {
        pairings.set(email, deviceId);
      });
    }
    console.log(`[data] loaded devices=${devices.size} pairings=${pairings.size}`);
  } catch (err) {
    console.log('[data] failed to load backend-data.json');
  }
};

loadData();

app.post('/api/auth/login', async (req, res) => {
  const { email, username, password } = req.body;
  const identifier = (email || username || '').trim();
  const forwarded = req.headers['x-forwarded-for'];
  const ip = forwarded ? forwarded.split(',')[0].trim() : req.socket.remoteAddress;
  console.log(`[parent login] id=${identifier} ip=${ip}`);

  if (!identifier) {
    return res.status(400).json({ error: 'Email or username is required' });
  }

  const user = users.get(identifier);
  if (!user) {
    return res.status(401).json({ error: 'Invalid credentials' });
  }

  if (password !== user.password) {
    return res.status(401).json({ error: 'Invalid credentials' });
  }

  const token = jwt.sign({ email: identifier }, JWT_SECRET, { expiresIn: '7d' });
  res.json({ token, email: identifier });
});

// Serve parent app if a production build exists
if (hasFrontendBuild) {
  app.use(express.static(buildPath));
  app.get('/', (req, res) => {
    res.sendFile(path.join(buildPath, 'index.html'));
  });
  app.get('*', (req, res, next) => {
    if (req.path.startsWith('/api') || req.path.startsWith('/socket.io')) {
      return next();
    }
    res.sendFile(path.join(buildPath, 'index.html'));
  });
} else {
  app.get('/', (req, res) => {
    res.json({
      message: 'Accountability Backend API',
      status: 'running',
      version: '1.0.0',
      note: 'Build the parent app (parent-app/build) to serve the UI here.'
    });
  });
}

app.post('/api/pair', authenticateToken, (req, res) => {
  const { pairingCode } = req.body;
  const { email } = req.user;

  console.log(`Pairing request from ${email} with code: ${pairingCode}`);
  
  if (!pairingCode || pairingCode.length !== 6) {
    return res.status(400).json({ error: 'Invalid pairing code format. Must be 6 digits.' });
  }

  // Find device with this pairing code
  let deviceId = null;
  for (const [id, device] of devices.entries()) {
    console.log(`Checking device ${id} with pairing code: ${device.pairingCode}`);
    if (device.pairingCode && device.pairingCode.toString() === pairingCode.toString()) {
      deviceId = id;
      console.log(`Found matching device: ${deviceId}`);
      break;
    }
  }

  if (!deviceId) {
    console.log(`No device found with pairing code: ${pairingCode}`);
    return res.status(404).json({ error: 'Invalid pairing code. Make sure the device is online and the code is correct.' });
  }

  // Check if device is online
  const device = devices.get(deviceId);
  if (!device || !device.online) {
    return res.status(404).json({ error: 'Device is offline. Make sure the child agent is running and connected.' });
  }

  // Create pairing
  pairings.set(email, deviceId);
  console.log(`Successfully paired ${email} with device ${deviceId}`);
  saveData();
  
  res.json({ message: 'Device paired successfully', deviceId });
});

app.get('/api/devices', authenticateToken, (req, res) => {
  const { email } = req.user;
  const deviceId = pairings.get(email);

  if (!deviceId) {
    return res.json({ devices: [] });
  }

  const device = devices.get(deviceId);
  if (!device) {
    return res.json({ devices: [] });
  }

  res.json({ devices: [{
    id: deviceId,
    name: device.name,
    online: device.online,
    lastSeen: device.lastSeen
  }] });
});

function authenticateToken(req, res, next) {
  const authHeader = req.headers['authorization'];
  const token = authHeader && authHeader.split(' ')[1];

  if (!token) {
    return res.sendStatus(401);
  }

  jwt.verify(token, JWT_SECRET, (err, user) => {
    if (err) return res.sendStatus(403);
    req.user = user;
    next();
  });
}

// WebSocket signaling
const deviceSockets = new Map();
const parentSockets = new Map();
const parentDeviceSockets = new Map();
const frameLogState = new Map();
const frameDropLogState = new Map();
const framePingLogState = new Map();
const getParentEmailForDevice = (deviceId) => {
  for (const [email, pairedDeviceId] of pairings.entries()) {
    if (pairedDeviceId === deviceId) return email;
  }
  return null;
};

io.on('connection', (socket) => {
  console.log('Client connected:', socket.id);
  const seenEvents = new Set();
  socket.onAny((event, ...args) => {
    if (event.startsWith('frame') && !seenEvents.has(event)) {
      seenEvents.add(event);
      const payloadType = args[0] === undefined ? 'undefined' : Array.isArray(args[0]) ? 'array' : typeof args[0];
      console.log(`[socket] event=${event} payloadType=${payloadType} socketId=${socket.id}`);
    }
  });

  socket.on('register-device', (payload) => {
    const deviceId = typeof payload === 'string' ? payload : payload?.deviceId;
    const reportedPairingCode = typeof payload === 'object' ? payload?.pairingCode : null;

    if (!deviceId) {
      console.log('[child connected] missing deviceId');
      return;
    }

    console.log(`[child connected] deviceId=${deviceId} socketId=${socket.id}`);
    if (reportedPairingCode) {
      console.log(`[child connected] device reported pairing code=${reportedPairingCode}`);
    }

    deviceSockets.set(deviceId, socket);
    
    // Preserve existing pairing code if device reconnects
    const existingDevice = devices.get(deviceId);
    const existingPairingCode = existingDevice ? existingDevice.pairingCode : null;
    const pairingCodeToUse = existingPairingCode || reportedPairingCode || null;
    
    devices.set(deviceId, {
      name: `Device ${deviceId.substring(0, 8)}`,
      online: true,
      lastSeen: new Date(),
      pairingCode: pairingCodeToUse,
      socketId: socket.id
    });
    saveData();
    
    socket.emit('device-registered', { deviceId });
    
    // Send existing pairing code back to device if it exists
    if (pairingCodeToUse) {
      console.log(`[child connected] pairing code=${pairingCodeToUse} deviceId=${deviceId}`);
      socket.emit('pairing-code', { pairingCode: pairingCodeToUse });
    } else {
      console.log(`[child connected] no pairing code yet for deviceId=${deviceId}`);
    }
  });

  socket.on('register-parent', (data) => {
    const { token } = data;
    
    try {
      const decoded = jwt.verify(token, JWT_SECRET);
      console.log(`[parent connected] email=${decoded.email} socketId=${socket.id}`);
      parentSockets.set(decoded.email, socket);
      socket.emit('parent-registered');
      
      // Send paired device info
      const deviceId = pairings.get(decoded.email);
      if (deviceId) {
        parentDeviceSockets.set(deviceId, socket);
        const device = devices.get(deviceId);
        if (device) {
          socket.emit('device-status', {
            deviceId,
            online: device.online
          });
        }
      }
    } catch (err) {
      socket.emit('error', { message: 'Invalid token' });
    }
  });

  // WebRTC signaling: offer from parent -> child
  socket.on('webrtc-offer', (data) => {
    const { token, offer } = data || {};
    try {
      const decoded = jwt.verify(token, JWT_SECRET);
      const deviceId = pairings.get(decoded.email);
      if (!deviceId) {
        socket.emit('error', { message: 'No paired device' });
        return;
      }

      const deviceSocket = deviceSockets.get(deviceId);
      if (!deviceSocket) {
        socket.emit('error', { message: 'Device offline' });
        return;
      }

      console.log(`[webrtc] offer from parent ${decoded.email} -> device ${deviceId}`);
      deviceSocket.emit('webrtc-offer', { offer, parentEmail: decoded.email });
    } catch (err) {
      socket.emit('error', { message: 'Invalid token' });
    }
  });

  // WebRTC signaling: answer from child -> parent
  socket.on('webrtc-answer', (data) => {
    const { deviceId, answer } = data || {};
    if (!deviceId) {
      socket.emit('error', { message: 'Missing deviceId' });
      return;
    }

    const parentEmail = getParentEmailForDevice(deviceId);
    if (!parentEmail) {
      socket.emit('error', { message: 'No paired parent' });
      return;
    }

    const parentSocket = parentSockets.get(parentEmail);
    if (!parentSocket) {
      socket.emit('error', { message: 'Parent offline' });
      return;
    }

    console.log(`[webrtc] answer from device ${deviceId} -> parent ${parentEmail}`);
    parentSocket.emit('webrtc-answer', { answer, deviceId });
  });

  // WebRTC signaling: ICE candidates (both directions)
  socket.on('webrtc-ice', (data) => {
    const { token, deviceId, candidate, target } = data || {};

    // If token is present, assume parent -> child
    if (token) {
      try {
        const decoded = jwt.verify(token, JWT_SECRET);
        const pairedDeviceId = pairings.get(decoded.email);
        if (!pairedDeviceId) {
          socket.emit('error', { message: 'No paired device' });
          return;
        }

        const deviceSocket = deviceSockets.get(pairedDeviceId);
        if (!deviceSocket) {
          socket.emit('error', { message: 'Device offline' });
          return;
        }

        console.log(`[webrtc] ice from parent ${decoded.email} -> device ${pairedDeviceId}`);
        deviceSocket.emit('webrtc-ice', { candidate, parentEmail: decoded.email });
        return;
      } catch (err) {
        socket.emit('error', { message: 'Invalid token' });
        return;
      }
    }

    // Otherwise assume child -> parent
    if (!deviceId) {
      socket.emit('error', { message: 'Missing deviceId' });
      return;
    }

    const parentEmail = getParentEmailForDevice(deviceId);
    if (!parentEmail) {
      socket.emit('error', { message: 'No paired parent' });
      return;
    }

    const parentSocket = parentSockets.get(parentEmail);
    if (!parentSocket) {
      socket.emit('error', { message: 'Parent offline' });
      return;
    }

    console.log(`[webrtc] ice from device ${deviceId} -> parent ${parentEmail}`);
    parentSocket.emit('webrtc-ice', { candidate, deviceId });
  });

  socket.on('request-stream', async (data) => {
    const { token } = data;
    
    try {
      console.log(`[view] request-stream received tokenPresent=${Boolean(token)} socketId=${socket.id}`);
      const decoded = jwt.verify(token, JWT_SECRET);
      const deviceId = pairings.get(decoded.email);
      
      if (!deviceId) {
        console.log(`[view] no paired device for parent=${decoded.email}`);
        socket.emit('error', { message: 'No paired device' });
        return;
      }

      const deviceSocket = deviceSockets.get(deviceId);
      if (!deviceSocket) {
        console.log(`[view] device offline deviceId=${deviceId} parent=${decoded.email}`);
        socket.emit('error', { message: 'Device offline' });
        return;
      }

      console.log(`[stream] started parent=${decoded.email} device=${deviceId}`);
      deviceSocket.emit('parent-viewing', { parentEmail: decoded.email });
      const requestId = `req-${Date.now()}`;
      deviceSocket.emit('request-stream', requestId);
      
      socket.emit('stream-requested', { requestId });
    } catch (err) {
      socket.emit('error', { message: 'Invalid token' });
    }
  });

  socket.on('stream-settings', (data) => {
    const { token, width, height, fps } = data || {};
    try {
      const decoded = jwt.verify(token, JWT_SECRET);
      const deviceId = pairings.get(decoded.email);
      if (!deviceId) {
        socket.emit('error', { message: 'No paired device' });
        return;
      }

      const deviceSocket = deviceSockets.get(deviceId);
      if (!deviceSocket) {
        socket.emit('error', { message: 'Device offline' });
        return;
      }

      deviceSocket.emit('stream-settings', { width, height, fps });
      console.log(`[stream] settings parent=${decoded.email} device=${deviceId} ${width}x${height}@${fps}`);
    } catch (err) {
      socket.emit('error', { message: 'Invalid token' });
    }
  });

  socket.on('stop-stream', (data) => {
    const { token } = data;
    
    try {
      const decoded = jwt.verify(token, JWT_SECRET);
      const deviceId = pairings.get(decoded.email);
      
      if (deviceId) {
        const deviceSocket = deviceSockets.get(deviceId);
        if (deviceSocket) {
          console.log(`[stream] ended parent=${decoded.email} device=${deviceId}`);
          deviceSocket.emit('stop-stream');
        }
      }
    } catch (err) {
      // Ignore
    }
  });

  // Frame relay fallback (JPEG data URLs over Socket.IO)
  socket.on('frame', (data) => {
    const payload = Array.isArray(data) ? data[0] : data;
    const { deviceId, dataUrl } = payload || {};
    if (!deviceId || !dataUrl) {
      if (!frameDropLogState.has('invalid-payload')) {
        frameDropLogState.set('invalid-payload', true);
        console.log(`[frame] drop invalid payload type=${typeof data} array=${Array.isArray(data)}`);
      }
      return;
    }

    const directParentSocket = parentDeviceSockets.get(deviceId);
    if (directParentSocket) {
      if (!frameLogState.has(deviceId)) {
        frameLogState.set(deviceId, true);
        console.log(`[frame] receiving frames device=${deviceId} size=${dataUrl.length}`);
      }
      directParentSocket.emit('frame', { deviceId, dataUrl });
      return;
    }

    const parentEmail = getParentEmailForDevice(deviceId);
    if (!parentEmail) {
      if (!frameDropLogState.has(`${deviceId}:noparent`)) {
        frameDropLogState.set(`${deviceId}:noparent`, true);
        console.log(`[frame] drop no paired parent device=${deviceId}`);
      }
      return;
    }

    const parentSocket = parentSockets.get(parentEmail);
    if (!parentSocket) {
      if (!frameDropLogState.has(`${deviceId}:parentoffline`)) {
        frameDropLogState.set(`${deviceId}:parentoffline`, true);
        console.log(`[frame] drop parent offline email=${parentEmail} device=${deviceId}`);
      }
      return;
    }

    if (!frameLogState.has(deviceId)) {
      frameLogState.set(deviceId, true);
      console.log(`[frame] receiving frames device=${deviceId} size=${dataUrl.length}`);
    }
    parentSocket.emit('frame', { deviceId, dataUrl });
  });

  socket.on('frame-ping', (data) => {
    const deviceId = data?.deviceId;
    if (!deviceId) return;
    if (!framePingLogState.has(deviceId)) {
      framePingLogState.set(deviceId, true);
      console.log(`[frame] ping received device=${deviceId} status=${data?.status ?? 'unknown'}`);
    }
  });

  socket.on('frame-test', (data) => {
    const deviceId = data?.deviceId;
    if (!deviceId) return;
    if (!framePingLogState.has(`${deviceId}:test`)) {
      framePingLogState.set(`${deviceId}:test`, true);
      console.log(`[frame] test received device=${deviceId} size=${data?.size ?? 'unknown'}`);
    }
  });

  socket.on('generate-pairing-code', (deviceId) => {
    const device = devices.get(deviceId);
    if (device) {
      // Only generate new code if device doesn't have one yet
      if (!device.pairingCode) {
        const pairingCode = Math.floor(100000 + Math.random() * 900000).toString();
        device.pairingCode = pairingCode;
        console.log(`[child pairing] generated code=${pairingCode} deviceId=${deviceId}`);
        socket.emit('pairing-code', { pairingCode });
        saveData();
      } else {
        // Device already has a pairing code, send it back
        console.log(`[child pairing] existing code=${device.pairingCode} deviceId=${deviceId}`);
        socket.emit('pairing-code', { pairingCode: device.pairingCode });
      }
    } else {
      console.log(`[child pairing] device not found deviceId=${deviceId}`);
    }
  });

  socket.on('disconnect', () => {
    console.log('Client disconnected:', socket.id);
    
    // Mark device as offline
    for (const [deviceId, device] of devices.entries()) {
      if (device.socketId === socket.id) {
        device.online = false;
        deviceSockets.delete(deviceId);
        parentDeviceSockets.delete(deviceId);
        break;
      }
    }

    for (const [email, parentSocket] of parentSockets.entries()) {
      if (parentSocket.id === socket.id) {
        const deviceId = pairings.get(email);
        if (deviceId) {
          const deviceSocket = deviceSockets.get(deviceId);
          if (deviceSocket) {
            console.log(`[stream] auto-stop parent-disconnect parent=${email} device=${deviceId}`);
            deviceSocket.emit('stop-stream');
          }
        }
        parentSockets.delete(email);
      }
    }

    for (const [deviceId, parentSocket] of parentDeviceSockets.entries()) {
      if (parentSocket.id === socket.id) {
        parentDeviceSockets.delete(deviceId);
      }
    }
  });
});

const PORT = process.env.PORT || 5000;
server.listen(PORT, () => {
  console.log(`Server running on port ${PORT}`);
});

