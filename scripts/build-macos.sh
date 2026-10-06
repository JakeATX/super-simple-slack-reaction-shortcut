#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD="$ROOT/build/macos"
APP="$BUILD/Super Simple Slack Reaction Shortcut.app"
mkdir -p "$APP/Contents/MacOS" "$ROOT/dist"
cp "$ROOT/macos/Info.plist" "$APP/Contents/Info.plist"
for arch in arm64 x86_64; do
  xcrun swiftc -O -target "$arch-apple-macos13.0" -framework AppKit -framework ApplicationServices -framework CoreGraphics \
    "$ROOT/macos/SlackReactionShortcut.swift" -o "$BUILD/helper-$arch"
done
lipo -create "$BUILD/helper-arm64" "$BUILD/helper-x86_64" -output "$APP/Contents/MacOS/SlackReactionShortcut"
codesign --force --sign - --identifier io.github.jakeatx.super-simple-slack-reaction-shortcut "$APP"
codesign --verify --strict "$APP"
"$APP/Contents/MacOS/SlackReactionShortcut" --self-test
ditto -c -k --keepParent "$APP" "$ROOT/dist/super-simple-slack-reaction-shortcut-macos.zip"
