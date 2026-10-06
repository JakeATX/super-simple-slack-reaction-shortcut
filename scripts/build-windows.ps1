$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'windows/SlackReactionShortcut.csproj'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
foreach ($arch in @('x64', 'arm64')) {
    $out = Join-Path $root "build/windows-$arch"
    dotnet publish $project -c Release -r "win-$arch" --self-contained true -o $out
    if ($LASTEXITCODE -ne 0) { throw "Windows $arch build failed" }
    Copy-Item (Join-Path $out 'SlackReactionShortcut.exe') (Join-Path $dist "super-simple-slack-reaction-shortcut-windows-$arch.exe")
}
$test = Start-Process -FilePath (Join-Path $dist 'super-simple-slack-reaction-shortcut-windows-x64.exe') -ArgumentList '--self-test' -Wait -PassThru
if ($test.ExitCode -ne 0) { throw 'Windows shortcut self-tests failed' }
Get-Content (Join-Path $env:TEMP 'slack-reaction-self-test.txt')
