# Super Simple Slack Reaction Shortcut

Open Slack’s message reaction picker with **⌘ + comma on Mac** or **Alt + comma on Windows**. This free, open-source helper installs with one command, starts when you log in, and leaves shortcuts in other apps alone.

## Mac — paste into Terminal

```bash
curl -fsSL https://raw.githubusercontent.com/JakeATX/super-simple-slack-reaction-shortcut/main/install-macos.sh | bash
```

**Turn on “Super Simple Slack Reaction Shortcut” in the Accessibility settings that open.** Done—go back to Slack and press **⌘,** to open reactions for a message. You can hover over or focus a particular message to target it.

Works on Apple Silicon and Intel Macs running macOS 13 or newer. No Homebrew, Xcode, or other app needed. macOS may ask for Touch ID or your computer password when you enable Accessibility; the helper cannot approve that for you.

## Windows — paste into PowerShell

```powershell
irm https://raw.githubusercontent.com/JakeATX/super-simple-slack-reaction-shortcut/main/install-windows.ps1 | iex
```

**Done—go back to Slack and press Alt + comma.** There is no Accessibility toggle on Windows. You can hover over or focus a particular message to target it.

Works on 64-bit Windows 10/11, including ARM64. No administrator access, AutoHotkey, or .NET installation needed.

## Pause or quit

Use the **⌘,** icon in the Mac menu bar or the helper icon in the Windows system tray. The helper starts again at your next login. Run the same install command to update or repair an installation.

## Uninstall

**Mac:**

```bash
curl -fsSL https://raw.githubusercontent.com/JakeATX/super-simple-slack-reaction-shortcut/main/uninstall-macos.sh | bash
```

**Windows:**

```powershell
irm https://raw.githubusercontent.com/JakeATX/super-simple-slack-reaction-shortcut/main/uninstall-windows.ps1 | iex
```

## If something doesn’t work

- **Mac says it needs permission:** enable this helper—not Terminal or Slack—in **System Settings → Privacy & Security → Accessibility**. After an update, macOS may need the toggle switched off and on again.
- **The picker doesn’t appear:** use the Slack **desktop app**, open a conversation, and hover over or focus a message. The helper translates your shortcut to Slack’s built-in **⌘⇧\** or **Ctrl⇧\**; keyboard layouts and Slack versions can vary. English keyboard layouts are the supported default.
- **Windows Slack runs as administrator:** launch Slack normally instead; Windows blocks input from a normal app into an elevated app.
- **A managed computer blocks the app:** ask your IT team. Installation does not disable Gatekeeper, SmartScreen, antivirus, or corporate policy. Release binaries are not notarized or commercially code-signed, so an OS security prompt is possible.

## Privacy and implementation

The helper processes keyboard events locally to recognize only the requested shortcut while Slack is the foreground app. It does not store typed text, send analytics, or connect to the network. Installers download prebuilt GitHub release files over HTTPS, check their SHA-256 checksums, install for the current user, and set up login startup.

macOS uses a native Swift event tap; Windows uses a native C# keyboard hook and restores the original Alt key state after generating Slack’s reaction shortcut. No paid software or background update service is required.

## Build from source

**Mac** (Xcode Command Line Tools required):

```bash
./scripts/build-macos.sh
```

**Windows** (.NET 8 SDK required):

```powershell
./scripts/build-windows.ps1
```

GitHub Actions builds both platforms, runs shortcut filtering and key-state tests, and packages release downloads. macOS contains both ARM64 and x86-64 slices; Windows ships separate x64 and ARM64 executables. Native macOS Slack behavior was tested; Windows packages run automated native tests, with live Windows Slack testing still welcome.

MIT licensed. Contributions and issues are welcome. Not affiliated with Slack or Salesforce.
