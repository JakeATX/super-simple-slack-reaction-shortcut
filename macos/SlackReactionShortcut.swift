import AppKit
import ApplicationServices
import CoreGraphics

let slackBundleID = "com.tinyspeck.slackmacgap"
let relevantFlags: CGEventFlags = [.maskCommand, .maskShift, .maskControl, .maskAlternate]
func shouldRemap(_ code: Int64, _ flags: CGEventFlags, _ bundle: String?) -> Bool {
    code == 43 && flags.intersection(relevantFlags) == .maskCommand && bundle == slackBundleID
}

if CommandLine.arguments.contains("--self-test") {
    precondition(shouldRemap(43, .maskCommand, slackBundleID))
    precondition(shouldRemap(43, [.maskCommand, .maskAlphaShift], slackBundleID))
    precondition(!shouldRemap(43, [.maskCommand, .maskShift], slackBundleID))
    precondition(!shouldRemap(43, [.maskCommand, .maskAlternate], slackBundleID))
    precondition(!shouldRemap(43, .maskCommand, "com.apple.finder"))
    precondition(!shouldRemap(43, [], slackBundleID))
    precondition(!shouldRemap(50, .maskCommand, slackBundleID))
    print("PASS: Slack-only Command+comma; other apps and modifier combinations unchanged")
    exit(0)
}

final class ShortcutApp: NSObject, NSApplicationDelegate {
    var statusItem: NSStatusItem!
    var statusLabel: NSMenuItem!
    var enableItem: NSMenuItem!
    var tap: CFMachPort?
    var source: CFRunLoopSource?
    var timer: Timer?
    var window: NSWindow?
    var enabled = true
    var mappedDown = false
    var count = 0
    var permissionRequested = false
    let support = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/Super Simple Slack Reaction Shortcut")

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        try? FileManager.default.createDirectory(at: support, withIntermediateDirectories: true)
        enabled = (UserDefaults.standard.object(forKey: "enabled") as? Bool) ?? true
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        statusItem.button?.title = "⌘,"
        statusItem.button?.toolTip = "Super Simple Slack Reaction Shortcut — Command+comma opens reactions in Slack"
        let menu = NSMenu()
        let title = NSMenuItem(title: "Slack: ⌘, → Reaction picker", action: nil, keyEquivalent: "")
        menu.addItem(title)
        statusLabel = NSMenuItem(title: "Checking Accessibility permission…", action: nil, keyEquivalent: "")
        menu.addItem(statusLabel)
        menu.addItem(.separator())
        enableItem = NSMenuItem(title: "Enable remap", action: #selector(toggle), keyEquivalent: "")
        enableItem.target = self
        menu.addItem(enableItem)
        let permissionItem = NSMenuItem(title: "Open Accessibility Settings…", action: #selector(openPermissions), keyEquivalent: "")
        permissionItem.target = self
        menu.addItem(permissionItem)
        let quitItem = NSMenuItem(title: "Quit", action: #selector(quit), keyEquivalent: "")
        quitItem.target = self
        menu.addItem(quitItem)
        statusItem.menu = menu
        refresh()
        if !AXIsProcessTrusted() {
            showSetup()
            // Register this app with TCC, then open the exact pane. Only request once per launch.
            openPermissions()
        }
        timer = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in self?.refresh() }
    }

    func refresh() {
        let trusted = AXIsProcessTrusted()
        if trusted && enabled && tap == nil { installTap() }
        if !trusted && tap != nil {
            if let source { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }
            if let tap { CFMachPortInvalidate(tap) }
            tap = nil
            source = nil
            mappedDown = false
        }
        if let tap = tap { CGEvent.tapEnable(tap: tap, enable: trusted && enabled) }
        enableItem.state = enabled ? .on : .off
        statusLabel.title = !trusted ? "Needs Accessibility permission" : !enabled ? "Paused" : tap != nil ? "Active — Slack only" : "Waiting for keyboard access"
        let payload: [String: Any] = ["accessibilityGranted": trusted, "enabled": enabled, "tapInstalled": tap != nil, "remappedPresses": count, "bundleID": slackBundleID, "updatedAt": Date().timeIntervalSince1970]
        if let data = try? JSONSerialization.data(withJSONObject: payload, options: [.prettyPrinted, .sortedKeys]) {
            try? data.write(to: support.appendingPathComponent("status.json"), options: .atomic)
        }
        if trusted && tap != nil { window?.close(); window = nil }
    }

    func installTap() {
        let mask = (CGEventMask(1) << CGEventType.keyDown.rawValue) | (CGEventMask(1) << CGEventType.keyUp.rawValue)
        tap = CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap, options: .defaultTap, eventsOfInterest: mask, callback: { _, type, event, context in
            guard let context else { return Unmanaged.passUnretained(event) }
            let app = Unmanaged<ShortcutApp>.fromOpaque(context).takeUnretainedValue()
            if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
                if let tap = app.tap, app.enabled && AXIsProcessTrusted() { CGEvent.tapEnable(tap: tap, enable: true) }
                return Unmanaged.passUnretained(event)
            }
            guard app.enabled else { return Unmanaged.passUnretained(event) }
            let code = event.getIntegerValueField(.keyboardEventKeycode)
            let bundle = NSWorkspace.shared.frontmostApplication?.bundleIdentifier
            let matched = shouldRemap(code, event.flags, bundle)
            if type == .keyDown && matched {
                app.mappedDown = true
                if event.getIntegerValueField(.keyboardEventAutorepeat) == 0 { app.count += 1 }
            } else if type == .keyUp && code == 43 && app.mappedDown {
                app.mappedDown = false
            } else {
                return Unmanaged.passUnretained(event)
            }
            event.setIntegerValueField(.keyboardEventKeycode, value: 42)
            event.flags = [.maskCommand, .maskShift]
            var character: UniChar = 124
            event.keyboardSetUnicodeString(stringLength: 1, unicodeString: &character)
            return Unmanaged.passUnretained(event)
        }, userInfo: Unmanaged.passUnretained(self).toOpaque())
        if let tap {
            source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
            if let source { CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes) }
            CGEvent.tapEnable(tap: tap, enable: true)
        }
    }

    func showSetup() {
        guard window == nil else { return }
        let panel = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 490, height: 210), styleMask: [.titled, .closable], backing: .buffered, defer: false)
        panel.title = "Super Simple Slack Reaction Shortcut"
        panel.isReleasedWhenClosed = false
        let label = NSTextField(wrappingLabelWithString: "Command + comma will open Slack’s reaction picker.\n\nEnable “Super Simple Slack Reaction Shortcut” in System Settings → Privacy & Security → Accessibility. The helper only changes this shortcut while Slack is active. It stores no typed text and uses no network connection.")
        label.frame = NSRect(x: 24, y: 65, width: 442, height: 125)
        panel.contentView?.addSubview(label)
        let button = NSButton(title: "Open Accessibility Settings", target: self, action: #selector(openPermissions))
        button.frame = NSRect(x: 24, y: 20, width: 235, height: 32)
        panel.contentView?.addSubview(button)
        window = panel
        panel.center()
        panel.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    @objc func openPermissions() {
        if !permissionRequested {
            permissionRequested = true
            let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
            _ = AXIsProcessTrustedWithOptions(options)
        }
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") { NSWorkspace.shared.open(url) }
    }
    @objc func toggle() { enabled.toggle(); UserDefaults.standard.set(enabled, forKey: "enabled"); refresh() }
    @objc func quit() { NSApp.terminate(nil) }
}
let app = NSApplication.shared
let delegate = ShortcutApp()
app.delegate = delegate
app.run()
