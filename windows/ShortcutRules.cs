using System.Runtime.InteropServices;

internal static class ShortcutRules
{
    public static bool Matches(int key, bool ctrl, bool shift, bool alt, bool win, string? processName) =>
        key == 0xBC && !ctrl && !shift && alt && !win &&
        string.Equals(processName, "Slack", StringComparison.OrdinalIgnoreCase);
}
