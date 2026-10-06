$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$repo = 'JakeATX/super-simple-slack-reaction-shortcut'
$base = "https://github.com/$repo/releases/latest/download"
$arch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64' -or $env:PROCESSOR_ARCHITEW6432 -eq 'ARM64') { 'arm64' } else { 'x64' }
if (-not [Environment]::Is64BitOperatingSystem) { throw '64-bit Windows 10 or newer is required.' }
$name = "super-simple-slack-reaction-shortcut-windows-$arch.exe"
$dir = Join-Path $env:LOCALAPPDATA 'SuperSimpleSlackReactionShortcut'
$stage = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $stage | Out-Null
try {
    Write-Host 'Downloading Super Simple Slack Reaction Shortcut...'
    $download = Join-Path $stage $name
    Invoke-WebRequest -UseBasicParsing "$base/$name" -OutFile $download
    $sums = (Invoke-WebRequest -UseBasicParsing "$base/SHA256SUMS").Content
    if ($sums -is [byte[]]) { $sums = [Text.Encoding]::UTF8.GetString($sums) }
    $expected = @($sums -split "`n" | ForEach-Object {
        $parts = $_.Trim() -split '\s+'
        if ($parts.Count -eq 2 -and $parts[1] -eq $name) { $parts[0] }
    })
    $actual = (Get-FileHash -Algorithm SHA256 $download).Hash.ToLowerInvariant()
    if ($expected.Count -ne 1 -or $actual -ne $expected[0].ToLowerInvariant()) { throw 'Download checksum did not match. Nothing installed.' }
    $test = Start-Process -FilePath $download -ArgumentList '--self-test' -Wait -PassThru
    if ($test.ExitCode -ne 0) { throw 'Downloaded helper failed its startup checks.' }
    if ($env:SLACK_REACTION_VERIFY_ONLY -eq '1') { Write-Host 'PASS: release download, checksum and native self-tests'; return }
    New-Item -ItemType Directory -Force $dir | Out-Null
    $exe = Join-Path $dir 'SlackReactionShortcut.exe'
    Get-Process -Name 'SlackReactionShortcut' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | Stop-Process
    Copy-Item $download $exe -Force
    $startup = [Environment]::GetFolderPath('Startup')
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut((Join-Path $startup 'Super Simple Slack Reaction Shortcut.lnk'))
    $link.TargetPath = $exe
    $link.WorkingDirectory = $dir
    $link.Description = 'Alt+comma opens Slack reactions'
    $link.Save()
    Start-Process $exe
    Write-Host 'Installed! Press Alt + comma in Slack. No permissions toggle is needed on Windows.'
    Write-Host 'It starts automatically when you log in. Use its tray icon to pause or quit.'
}
finally { Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue }
