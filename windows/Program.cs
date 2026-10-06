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
        pause = new ToolStripMenuItem("Enable remap") { Checked = true };
        pause.Click += (_, _) => { enabled = !enabled; pause.Checked = enabled; };
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


