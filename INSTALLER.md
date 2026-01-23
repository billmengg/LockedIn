# Installer & Deployment (Child Agent)

This turns the child agent into a real Windows app with an installer and startup behavior.

## 1) Build the app (self-contained)

```powershell
.\scripts\publish.ps1
```

Output goes to:
```
child-agent\publish\
```

## 2) Build the installer (Inno Setup)

Install Inno Setup: https://jrsoftware.org/isinfo.php

Then:
```powershell
.\scripts\build-installer.ps1
```

Installer output:
```
installer\out\AccountabilityAgentSetup.exe
```

## 3) Install on another machine

1. Copy `AccountabilityAgentSetup.exe` to the target computer.
2. Run the installer.
3. The app will:
   - Install to `Program Files\AccountabilityAgent`
   - Create a Start Menu entry
   - Add itself to Startup (runs on login)

## 4) Point the child agent to your server

On the child machine, set the backend URL (public IP):

**Option A:** Create file:
```
%LocalAppData%\AccountabilityAgent\server.url
```
Example:
```
http://YOUR_PUBLIC_IP:5000
```

**Option B:** Env var
```
ACCOUNTABILITY_SERVER_URL=http://YOUR_PUBLIC_IP:5000
```

## 5) Debug mode (optional)

To show the console window:
```
AccountabilityAgent.exe /debug
```

Or:
```
set ACCOUNTABILITY_DEBUG=1
```

## Notes

- The installer uses Inno Setup.
- Startup entry is added automatically.
- The child agent runs minimized by default.
