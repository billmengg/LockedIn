param(
  [string]$Configuration = "Release",
  [string]$InnoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"

& "$PSScriptRoot\publish.ps1" -Configuration $Configuration

$iss = Join-Path $PSScriptRoot "..\installer\AccountabilityAgent.iss"
if (!(Test-Path $InnoSetupPath)) {
  Write-Host "Inno Setup not found at $InnoSetupPath"
  Write-Host "Install Inno Setup or pass -InnoSetupPath."
  exit 1
}

& $InnoSetupPath $iss
Write-Host "Installer built."
