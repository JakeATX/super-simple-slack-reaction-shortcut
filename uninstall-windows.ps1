$ErrorActionPreference = 'Stop'
$dir = Join-Path $env:LOCALAPPDATA 'SuperSimpleSlackReactionShortcut'
$exe = Join-Path $dir 'SlackReactionShortcut.exe'
$running = @(Get-Process -Name 'SlackReactionShortcut' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe })
foreach ($process in $running) {
    Stop-Process -InputObject $process -Force
    if (!$process.WaitForExit(10000)) { throw 'The existing helper did not exit. Try again.' }
}
$link = Join-Path ([Environment]::GetFolderPath('Startup')) 'Super Simple Slack Reaction Shortcut.lnk'
Remove-Item $link -Force -ErrorAction SilentlyContinue
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
Write-Host 'Uninstalled.'
