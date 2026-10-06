using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            ShortcutTests.Run();
            return 0;
        }
        using var mutex = new Mutex(true, "Local\\SuperSimpleSlackReactionShortcut", out var first);
        if (!first) return 0;
        ApplicationConfiguration.Initialize();
        using var context = new ShortcutContext();
        Application.Run(context);
        return 0;
    }
}

internal static class ShortcutRules
{
    public static bool Matches(int key, bool ctrl, bool shift, bool alt, bool win, string? processName) =>
        key == 0xBC && !ctrl && !shift && alt && !win &&
        string.Equals(processName, "Slack", StringComparison.OrdinalIgnoreCase);
}

internal static class ShortcutTests
{
    public static void Run()
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        Check(ShortcutRules.Matches(0xBC, false, false, true, false, "Slack"), "Alt+comma in Slack must match");
        Check(!ShortcutRules.Matches(0xBC, false, false, true, false, "notepad"), "Other apps must remain unchanged");
        Check(!ShortcutRules.Matches(0xBC, false, true, true, false, "Slack"), "Alt+Shift+comma must remain unchanged");
        Check(!ShortcutRules.Matches(0xBC, true, false, true, false, "Slack"), "Ctrl+Alt/AltGr+comma must remain unchanged");
        Check(!ShortcutRules.Matches(0xBC, false, false, true, true, "Slack"), "Windows modifier must remain unchanged");
        Check(!ShortcutRules.Matches(0xBC, false, false, false, false, "Slack"), "Plain comma must remain unchanged");
        Check(!ShortcutRules.Matches(0xBC, false, false, false, false, "Slack"), "Ctrl+comma must remain unchanged");
        Check(!ShortcutRules.Matches(0xBE, false, false, true, false, "Slack"), "Alt+period must remain unchanged");
        Check(Marshal.SizeOf<Native.INPUT>() == (IntPtr.Size == 8 ? 40 : 28), "SendInput ABI size mismatch");
        foreach (var sides in new[] { (true, false), (false, true), (true, true) })
        {
            var events = Native.ReactionEvents(sides.Item1, sides.Item2);
            var held = new HashSet<ushort>();
            if (sides.Item1) held.Add(0xA4);
            if (sides.Item2) held.Add(0xA5);
            foreach (var input in events)
            {
                var k = input.data.keyboard;
                if (k.dwFlags == 2) held.Remove(k.wVk); else held.Add(k.wVk);
                if (k.wVk == 0xDC && k.dwFlags == 0)
                    Check(held.Contains(0x11) && held.Contains(0x10) && !held.Contains(0xA4) && !held.Contains(0xA5), "Slack must receive Ctrl+Shift+backslash with Alt released");
            }
            Check(!held.Contains(0x11) && !held.Contains(0x10) && !held.Contains(0xDC), "Injected Ctrl, Shift and backslash must be released");
            Check(held.Contains(0xA4) == sides.Item1 && held.Contains(0xA5) == sides.Item2, "Original Alt state must be restored");
        }
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "slack-reaction-self-test.txt"), "PASS: Slack-only Alt+comma, modifier exclusions, SendInput ABI, balanced key release");
    }
}

internal sealed class ShortcutContext : ApplicationContext
{
    private readonly Native.HookProc callback;
    private readonly NotifyIcon tray;
    private readonly ToolStripMenuItem pause;
    private IntPtr hook;
    private bool enabled = true;
    private bool commaHeld;

    public ShortcutContext()
    {
        callback = OnKey;
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Slack: Alt+, → Reaction picker") { Enabled = false });
        pause = new ToolStripMenuItem("Enable remap", null, (_, _) => { enabled = !enabled; pause.Checked = enabled; }) { Checked = true };
        menu.Items.Add(pause);
        menu.Items.Add("Quit", null, (_, _) => ExitThread());
        tray = new NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "Slack reaction: Alt+,", ContextMenuStrip = menu, Visible = true };
        hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not install the Slack keyboard shortcut hook");
        tray.ShowBalloonTip(4000, "Super Simple Slack Reaction Shortcut", "Ready! Press Alt + comma in Slack to open reactions. Other apps are unchanged.", ToolTipIcon.Info);
    }

    private IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (code >= 0)
            {
                var key = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
                // Never reprocess generated keys or alter ordinary typing.
                if ((key.flags & 0x10) == 0 && key.vkCode == 0xBC)
                {
                    int message = wParam.ToInt32();
                    bool down = message == 0x100 || message == 0x104;
                    bool up = message == 0x101 || message == 0x105;
                    if (up && commaHeld) { commaHeld = false; return (IntPtr)1; }
                    if (down && commaHeld) return (IntPtr)1;
                    if (down && enabled && !Native.Down(0x11) && !Native.Down(0x10) && Native.Down(0x12) && !Native.Down(0x5B) && !Native.Down(0x5C))
                    {
                        var window = Native.GetForegroundWindow();
                        Native.GetWindowThreadProcessId(window, out var pid);
                        using var process = Process.GetProcessById((int)pid);
                        if (ShortcutRules.Matches((int)key.vkCode, false, false, true, false, process.ProcessName))
                        {
                            bool leftAlt = Native.Down(0xA4);
                            bool rightAlt = Native.Down(0xA5);
                            var inputs = Native.ReactionEvents(leftAlt, rightAlt);
                            var sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.INPUT>());
                            if (sent == inputs.Length) { commaHeld = true; return (IntPtr)1; }
                            // Release synthetic keys and restore held Alt if injection was only partly delivered.
                            var release = Native.RecoveryEvents(leftAlt, rightAlt);
                            Native.SendInput((uint)release.Length, release, Marshal.SizeOf<Native.INPUT>());
                        }
                    }
                }
            }
        }
        catch { /* Permission/process race: let the user's original key through. */ }
        return Native.CallNextHookEx(hook, code, wParam, lParam);
    }

    protected override void ExitThreadCore()
    {
        if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }
}

internal static class Native
{
    internal delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] internal struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion { [FieldOffset(0)] public KEYBDINPUT keyboard; [FieldOffset(0)] public MOUSEINPUT mouse; }
    [StructLayout(LayoutKind.Sequential)] internal struct INPUT { public uint type; public InputUnion data; }
    internal static INPUT Key(ushort key, bool up = false) => new() { type = 1, data = new InputUnion { keyboard = new KEYBDINPUT { wVk = key, dwFlags = up ? 2u : 0u } } };
    internal static INPUT[] ReactionEvents(bool leftAlt, bool rightAlt)
    {
        // Hold Ctrl while releasing/restoring Alt so Windows does not treat this as a bare Alt menu gesture.
        var inputs = new List<INPUT> { Key(0x11) };
        if (leftAlt) inputs.Add(Key(0xA4, true));
        if (rightAlt) inputs.Add(Key(0xA5, true));
        inputs.AddRange(new[] { Key(0x10), Key(0xDC), Key(0xDC, true), Key(0x10, true) });
        if (leftAlt) inputs.Add(Key(0xA4));
        if (rightAlt) inputs.Add(Key(0xA5));
        inputs.Add(Key(0x11, true));
        return inputs.ToArray();
    }
    internal static INPUT[] RecoveryEvents(bool leftAlt, bool rightAlt)
    {
        var inputs = new List<INPUT> { Key(0xDC, true), Key(0x10, true) };
        if (leftAlt) inputs.Add(Key(0xA4));
        if (rightAlt) inputs.Add(Key(0xA5));
        inputs.Add(Key(0x11, true));
        return inputs.ToArray();
    }
    internal static bool Down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string? moduleName);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, INPUT[] inputs, int size);
}
