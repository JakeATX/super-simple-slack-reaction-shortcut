#!/bin/bash
set -euo pipefail
REPO='JakeATX/super-simple-slack-reaction-shortcut'
BASE="https://github.com/$REPO/releases/latest/download"
APP_NAME='Super Simple Slack Reaction Shortcut'
LABEL='io.github.jakeatx.super-simple-slack-reaction-shortcut'
TASK_HOME="${SLACK_REACTION_HOME:-$HOME}"
TASK_TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/slack-reaction-install.XXXXXX")"
trap 'rm -rf "$TASK_TMP_DIR"' EXIT
if [[ "$(uname -s)" != Darwin ]]; then echo 'This installer is for macOS. Use the Windows command in the README.' >&2; exit 1; fi
if (( $(sw_vers -productVersion | cut -d. -f1) < 13 )); then echo 'macOS 13 Ventura or newer is required.' >&2; exit 1; fi
printf 'Downloading %s…\n' "$APP_NAME"
curl -fLsS --retry 3 "$BASE/super-simple-slack-reaction-shortcut-macos.zip" -o "$TASK_TMP_DIR/app.zip"
curl -fLsS --retry 3 "$BASE/SHA256SUMS" -o "$TASK_TMP_DIR/SHA256SUMS"
EXPECTED="$(awk '$2 == "super-simple-slack-reaction-shortcut-macos.zip" { print $1 }' "$TASK_TMP_DIR/SHA256SUMS")"
ACTUAL="$(shasum -a 256 "$TASK_TMP_DIR/app.zip" | awk '{print $1}')"
[[ -n "$EXPECTED" && "$EXPECTED" == "$ACTUAL" ]] || { echo 'Download checksum did not match. Nothing installed.' >&2; exit 1; }
ditto -x -k "$TASK_TMP_DIR/app.zip" "$TASK_TMP_DIR/unpacked"
APP="$TASK_TMP_DIR/unpacked/$APP_NAME.app"
codesign --verify --strict "$APP"
"$APP/Contents/MacOS/SlackReactionShortcut" --self-test
if [[ "${SLACK_REACTION_VERIFY_ONLY:-0}" == 1 ]]; then echo 'PASS: release download, checksum, signature and native self-tests'; exit 0; fi
DEST="$TASK_HOME/Applications/$APP_NAME.app"
AGENT="$TASK_HOME/Library/LaunchAgents/$LABEL.plist"
mkdir -p "$TASK_HOME/Applications" "$TASK_HOME/Library/LaunchAgents" "$TASK_HOME/Library/Logs"
launchctl bootout "gui/$(id -u)/$LABEL" >/dev/null 2>&1 || true
# Stop only this helper's installed binary, including an instance opened from Finder.
pgrep -f "^$DEST/Contents/MacOS/SlackReactionShortcut$" | while read -r pid; do kill "$pid" 2>/dev/null || true; done || true
# Stage the replacement before changing an existing installation.
STAGED="$TASK_HOME/Applications/.$APP_NAME-install.app"
[[ ! -L "$DEST" && ! -L "$STAGED" ]] || { echo 'Refusing to replace a symlink at the install path.' >&2; exit 1; }
if [[ -d "$STAGED" ]]; then rm -rf "$STAGED"; fi
ditto "$APP" "$STAGED"
if [[ -d "$DEST" ]]; then rm -rf "$DEST"; fi
mv "$STAGED" "$DEST"
# plutil safely handles spaces and special characters in the home-directory path.
cat > "$AGENT" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>Label</key><string>io.github.jakeatx.super-simple-slack-reaction-shortcut</string>
<key>ProgramArguments</key><array/>
<key>RunAtLoad</key><true/>
<key>ProcessType</key><string>Interactive</string>
</dict></plist>
PLIST
plutil -insert ProgramArguments.0 -string "$DEST/Contents/MacOS/SlackReactionShortcut" "$AGENT"
plutil -insert StandardOutPath -string "$TASK_HOME/Library/Logs/SlackReactionShortcut.log" "$AGENT"
plutil -insert StandardErrorPath -string "$TASK_HOME/Library/Logs/SlackReactionShortcut-error.log" "$AGENT"
plutil -lint "$AGENT"
launchctl bootstrap "gui/$(id -u)" "$AGENT"
printf '\nInstalled! Enable “%s” in the Accessibility pane that opens.\n' "$APP_NAME"
printf 'Then press Command + comma in Slack. It starts automatically when you log in.\n'
