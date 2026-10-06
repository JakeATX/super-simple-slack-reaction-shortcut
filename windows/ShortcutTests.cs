using System.Runtime.InteropServices;

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
        Check(!ShortcutRules.Matches(0xBC, true, false, false, false, "Slack"), "Ctrl+comma must remain unchanged");
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
                if ((k.dwFlags & 2) != 0) held.Remove(k.wVk); else held.Add(k.wVk);
                if (k.wVk == 0xDC && (k.dwFlags & 2) == 0)
                    Check(held.Contains(0x11) && held.Contains(0x10) && !held.Contains(0xA4) && !held.Contains(0xA5), "Slack must receive Ctrl+Shift+backslash with Alt released");
            }
            Check(!held.Contains(0x11) && !held.Contains(0x10) && !held.Contains(0xDC), "Injected Ctrl, Shift and backslash must be released");
            Check(held.Contains(0xA4) == sides.Item1 && held.Contains(0xA5) == sides.Item2, "Original Alt state must be restored");
        }
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "slack-reaction-self-test.txt"), "PASS: Slack-only Alt+comma, modifier exclusions, SendInput ABI, balanced key release");
    }
}
