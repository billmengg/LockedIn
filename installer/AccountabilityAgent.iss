[Setup]
AppName=Accountability Agent
AppVersion=1.0.1
DefaultDirName={pf}\AccountabilityAgent
DefaultGroupName=Accountability Agent
OutputDir=..\installer\out
OutputBaseFilename=AccountabilityAgentSetup
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "..\child-agent\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\Accountability Agent"; Filename: "{app}\AccountabilityAgent.exe"; Parameters: "/minimized"
Name: "{userstartup}\Accountability Agent"; Filename: "{app}\AccountabilityAgent.exe"; Parameters: "/minimized"

[Run]
Filename: "{app}\AccountabilityAgent.exe"; Parameters: "/minimized"; Flags: nowait postinstall skipifsilent
