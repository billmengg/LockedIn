# Testing Guide

## What You Can Test RIGHT NOW (No Additional Implementation)

### ✅ Fully Functional - Ready for Testing

1. **Authentication Flow**
   - Register parent account
   - Login/Logout
   - Token storage and validation

2. **Device Pairing**
   - Generate pairing code on child device
   - Pair device via parent app
   - Device registration with backend

3. **Signaling & Connection**
   - Device online/offline status
   - Stream request signaling (request sent → child receives it)
   - Overlay indicator appears on child device
   - Auto-disconnect after 15 minutes

4. **Screen Capture** (Limited Visibility)
   - Frames are being captured at 10 FPS
   - Check debug output/console logs to verify
   - Statistics tracking (frames, FPS, duration)

### ❌ What's Missing - Cannot Test Yet

1. **WebRTC Video Streaming** - Priority 2
   - No video transmission from child to parent
   - No peer-to-peer connection
   - No offer/answer exchange
   - No ICE candidate exchange

2. **Video Display**
   - Parent app shows alert instead of video
   - No `<video>` element displaying stream
   - No WebRTC remote stream handling

---

## To Start Testing NOW

### Minimum Setup (Test Authentication & Pairing)

1. **Start Backend:**
   ```bash
   cd backend
   npm install
   npm start
   ```
   - Server runs on `http://localhost:5000`
   - Verify: See "Server running on port 5000"

2. **Start Parent App:**
   ```bash
   cd parent-app
   npm install
   npm start
   ```
   - App opens at `http://localhost:3000`
   - Verify: Login page loads

3. **Start Child Agent:**
   ```bash
   cd child-agent
   dotnet restore
   dotnet build
   dotnet run
   ```
   - Verify: System tray icon appears (green = connected)
   - Verify: Status shows "Connected"

### Test Scenarios You Can Run Now

#### Test 1: Authentication
1. Open parent app → Register new account
2. Login with credentials
3. Verify: Dashboard loads
4. Logout → Verify: Returns to login

#### Test 2: Device Pairing
1. Right-click child agent tray icon → "Generate Pairing Code"
2. In parent app → Click "Pair Device"
3. Enter 6-digit code
4. Verify: Device appears in dashboard with "Online" status

#### Test 3: Stream Request Signaling
1. Click "View Screen" button in parent app
2. Verify: Child device shows overlay "Parent check-in active"
3. Verify: Child tray icon turns orange
4. Verify: Parent app shows "Streaming..." (but no video)
5. Check child agent debug output → Should see frame capture logs

#### Test 4: Live Stream (JPEG frames over WebRTC data channel)
1. Open the parent app
2. Click "View Screen"
3. Verify: Video area shows updating image frames
4. Verify: Child console shows "Screen capture started"
5. If no frames appear, check backend logs for:
   - `[webrtc] offer from parent`
   - `[webrtc] answer from device`
   - `[webrtc] ice from parent/device`

Note: This is a temporary streaming mode using JPEG frames over a data channel (~2 FPS).
It confirms end-to-end WebRTC signaling and data flow.

#### Test 4: Screen Capture Verification
1. Request stream
2. Check child agent console/debug output
3. Look for: "Frame captured: [width]x[height], Total frames: [count]"
4. Verify frames are being captured every 100ms (~10 FPS)

---

## To Test FULL END-TO-END (Needs Implementation)

### What You Need to Implement:

#### Priority 2A: WebRTC on Parent Side (React)
✅ Implemented
- `parent-app/src/utils/webrtc.js`
- `parent-app/src/components/VideoStream.js`
- `parent-app/src/components/Dashboard.js`

Supports:
- WebRTC offer/answer via backend
- ICE candidate handling
- Video element fallback to frame image

#### Priority 2B: WebRTC on Child Side (C#)
🔄 Partially implemented (data channel frames)
- `child-agent/WebRtcService.cs`
- `child-agent/MainForm.cs`

Supports:
- WebRTC offer handling
- Answer generation
- ICE candidate exchange
- JPEG frames over data channel (~2 FPS)

Still needed:
- Real video track (H.264/VP8)

#### Priority 2C: ICE Candidate Exchange via Signaling
✅ Implemented
- `backend/server.js`
- `child-agent/MainForm.cs`
- `parent-app/src/components/Dashboard.js`

---

## Recommended Testing Order

### Phase 1: Current Testing (No New Code)
✅ Test authentication flow
✅ Test device pairing
✅ Verify signaling works
✅ Verify screen capture (via debug logs)
✅ Test overlay indicator

**Time:** 15-30 minutes

### Phase 2: After WebRTC Implementation
1. Test video stream appears in parent app
2. Test stream quality (check FPS, resolution)
3. Test reconnection if connection drops
4. Test multiple stream sessions
5. Test auto-disconnect timer

**Estimated Time:** After 2-3 days of WebRTC implementation

---

## Debug/Verification Points

### Backend (Node.js)
- Check console for: "Client connected", "Device registered"
- Verify Socket.IO events are received/sent
- Check for authentication errors

### Parent App (React)
- Open browser DevTools → Console tab
- Check Network tab for WebSocket connection
- Verify API calls succeed (200 status)
- Check for WebRTC errors (after implementation)

### Child Agent (C#)
- Check Debug output in Visual Studio or console
- Look for: "Screen capture started", "Frame captured"
- Verify frame statistics (Frames: X, FPS: Y)
- Check Socket.IO connection status

---

## Quick Verification Checklist

Before testing, verify:

- [ ] Backend server is running (port 5000)
- [ ] Parent app is running (port 3000)
- [ ] Child agent is running (tray icon visible)
- [ ] All three can communicate (check console logs)
- [ ] Parent account is registered
- [ ] Device is paired

During testing, verify:

- [ ] Parent app shows device status as "Online"
- [ ] Stream request triggers overlay on child device
- [ ] Screen capture is running (check debug output)
- [ ] Auto-disconnect works after 15 minutes

---

## Known Limitations (Before Full Video Track)

1. **No real video track** - Streaming uses JPEG frames over data channel
2. **Lower frame rate** - ~2 FPS, not real-time video quality
3. **No quality metrics** - Cannot test real video quality yet
4. **No audio** - Audio is not implemented

---

## Next Steps

Once you've verified everything above works, implement:
1. WebRTC peer connection (both sides)
2. ICE candidate exchange
3. Video display component
4. Connection state management

Then you can test the full end-to-end video streaming!

