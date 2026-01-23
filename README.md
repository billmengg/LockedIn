# Accountability Screen Check-In

Transparent, ethical screen check-in software for people who want a trusted accountability partner to help with motivation and focus.

## Overview

This MVP implements a three-component system:

1. **User Agent** - Windows desktop app that runs on the user's device
2. **Accountability Web App** - React web application for the trusted viewer
3. **Backend Server** - Node.js WebSocket signaling server

## Architecture

```
Accountability Web App (React)
      |
      |  (request view)
      v
Backend Server (Node.js + Socket.IO)
      |
      v
User Agent (C# .NET Windows App)
      |
      |  WebRTC (encrypted video)
      v
Accountability Web App
```

## Features

- ✅ Viewer-initiated screen viewing
- ✅ Visible indicator on user device during viewing
- ✅ System tray icon with status
- ✅ One-time device pairing
- ✅ Authentication system
- ✅ Auto-disconnect after 15 minutes

## Prerequisites

- **Node.js** 18+ (for backend and parent app)
- **.NET 8.0 SDK** (for child agent)
- **Windows 10/11** (for child agent)

## Setup Instructions

### 1. Backend Server

```bash
cd backend
npm install
npm start
```

The server will run on `http://localhost:5000`

### 2. Accountability Web App

In a new terminal:

```bash
cd parent-app
npm install
npm start
```

The app will open at `http://localhost:3000`

**Note:** Create a `.env` file in `parent-app/` if you need to change the API URL:
```
REACT_APP_API_URL=http://localhost:5000
```

### 3. User Agent

```bash
cd child-agent
dotnet restore
dotnet build
dotnet run
```

Or to run minimized:
```bash
dotnet run -- /minimized
```

## Usage

### First Time Setup

1. **Sign In (Accountability Partner)**
   - Open the accountability web app at `http://localhost:3000`
   - Use the credentials in `account.txt` (email or username)

2. **Pair User Device**
   - Run the user agent on the user's Windows computer
   - Right-click the system tray icon
   - Click "Generate Pairing Code"
   - In the accountability web app, click "Pair Device"
   - Enter the 6-digit pairing code
   - Device is now paired

### Viewing Screen

1. Open the accountability web app
2. Ensure your paired device shows "Online" status
3. Click "View Screen" button
4. The user device will show an orange overlay: "Check-in active"
5. Connection auto-disconnects after 15 minutes

## Development

### Backend

The backend uses in-memory storage for MVP. For production:
- Replace with a database (PostgreSQL, MongoDB, etc.)
- Use environment variables for JWT secret
- Add HTTPS support
- Implement proper error logging

### User Agent

The child agent currently has placeholder screen capture. For full implementation:
- Integrate Windows Graphics Capture API
- Implement WebRTC peer-to-peer streaming
- Add startup integration (registry or Windows service)

### Accountability App

The parent app has basic WebRTC signaling. For full implementation:
- Add WebRTC client-side video display
- Implement MediaRecorder for optional recording
- Add reconnection logic

## Project Structure

```
Accountability/
├── backend/              # Node.js signaling server
│   ├── server.js        # Main server file
│   └── package.json
├── parent-app/          # React web application (accountability viewer)
│   ├── public/
│   ├── src/
│   │   ├── components/  # React components
│   │   ├── utils/       # API and auth utilities
│   │   └── App.js
│   └── package.json
├── child-agent/         # C# Windows application (user agent)
│   ├── Program.cs
│   ├── MainForm.cs      # Main form with tray icon
│   ├── OverlayForm.cs   # Streaming indicator
│   └── AccountabilityAgent.csproj
└── plan.txt             # Original plan document
```

## Security Notes

⚠️ **This is MVP software for development/testing**

- Change JWT_SECRET in production
- Use HTTPS in production
- Implement proper database instead of in-memory storage
- Add input validation and sanitization
- Implement rate limiting
- Use signed binaries for child agent

## License

See LICENSE file (if applicable)

## Future Enhancements

- macOS support
- Multiple users per accountability partner
- Time-boxed viewing sessions
- Usage summaries (non-visual)
- Age-based permission scaling

