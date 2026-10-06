#!/bin/bash
set -euo pipefail
NAME='Super Simple Slack Reaction Shortcut'
LABEL='io.github.jakeatx.super-simple-slack-reaction-shortcut'
DEST="$HOME/Applications/$NAME.app"
launchctl bootout "gui/$(id -u)/$LABEL" >/dev/null 2>&1 || true
pgrep -f "^$DEST/Contents/MacOS/SlackReactionShortcut$" | while read -r pid; do kill "$pid" 2>/dev/null || true; done || true
[[ ! -L "$DEST" ]] || { echo 'Refusing to remove a symlink.' >&2; exit 1; }
rm -rf "$DEST"
rm -f "$HOME/Library/LaunchAgents/$LABEL.plist"
echo 'Uninstalled. You can remove its old entry from Accessibility settings.'
