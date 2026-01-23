param(
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\child-agent\AccountabilityAgent.csproj"
$outDir = Join-Path $PSScriptRoot "..\child-agent\publish"

dotnet publish $project `
  -c $Configuration `
  -r win-x64 `
  --self-contained true `
  /p:PublishTrimmed=false `
  -o $outDir

Write-Host "Published to $outDir"
