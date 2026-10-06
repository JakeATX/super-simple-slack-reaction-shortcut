using System.Runtime.InteropServices;

internal static class Native
{
    internal delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] internal struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion { [FieldOffset(0)] public KEYBDINPUT keyboard; [FieldOffset(0)] public MOUSEINPUT mouse; }
    [StructLayout(LayoutKind.Sequential)] internal struct INPUT { public uint type; public InputUnion data; }
    internal static INPUT Key(ushort key, bool up = false) => new() { type = 1, data = new InputUnion { keyboard = new KEYBDINPUT { wVk = key, dwFlags = (up ? 2u : 0u) | (key == 0xA5 ? 1u : 0u) } } };
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
