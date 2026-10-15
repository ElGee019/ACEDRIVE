using System.Runtime.InteropServices;

namespace AceDrive.Overlay.Services;

/// <summary>Win32 interop: click-through toggle + global hotkey.</summary>
internal static class Native
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int HOTKEY_ID = 0xACED;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint VK_C = 0x43;
    public const int WM_HOTKEY = 0x0312;
    public const int WM_ENTERSIZEMOVE = 0x0231;
    public const int WM_EXITSIZEMOVE = 0x0232;

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static void SetClickThrough(IntPtr hWnd, bool clickThrough)
    {
        int style = GetWindowLong(hWnd, GWL_EXSTYLE);
        style = clickThrough
            ? style | WS_EX_TRANSPARENT
            : style & ~WS_EX_TRANSPARENT;
        _ = SetWindowLong(hWnd, GWL_EXSTYLE, style);
    }
}
