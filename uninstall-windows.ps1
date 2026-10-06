$ErrorActionPreference = 'Stop'
$dir = Join-Path $env:LOCALAPPDATA 'SuperSimpleSlackReactionShortcut'
$exe = Join-Path $dir 'SlackReactionShortcut.exe'
Get-Process -Name 'SlackReactionShortcut' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | Stop-Process
$link = Join-Path ([Environment]::GetFolderPath('Startup')) 'Super Simple Slack Reaction Shortcut.lnk'
Remove-Item $link -Force -ErrorAction SilentlyContinue
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
Write-Host 'Uninstalled.'
