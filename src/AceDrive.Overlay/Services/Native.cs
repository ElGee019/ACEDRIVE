using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AceDrive.Overlay.Services;

/// <summary>Win32 interop: click-through toggle + global hotkey.</summary>
internal static class Native
{
    private const string GameProcessName = "AssettoCorsaEVO";

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int HOTKEY_ID = 0xACED;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint VK_C = 0x43;
    public const int WM_HOTKEY = 0x0312;
    public const int WM_ENTERSIZEMOVE = 0x0231;
    public const int WM_EXITSIZEMOVE = 0x0232;
    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int MA_NOACTIVATE = 3;

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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    /// <summary>True while <paramref name="hWnd"/> owns the foreground.</summary>
    public static bool IsForeground(IntPtr hWnd) => GetForegroundWindow() == hWnd;

    /// <summary>
    /// Hands the foreground back to EVO after an overlay click, so driving
    /// input resumes without an extra click into the game. Best-effort: with
    /// no game window there is nothing to focus, and Windows refusing the
    /// hand-over must never break the click that triggered it.
    /// </summary>
    public static void FocusGame()
    {
        IntPtr game = GameWindow();
        if (game == IntPtr.Zero || IsForeground(game))
        {
            return;
        }

        // WHY attach: Windows only grants SetForegroundWindow to the process
        // that owns the foreground or received the last input event. The
        // deferred hand-over can run a turn after our click, so borrowing the
        // foreground thread's input queue keeps the call reliable when the
        // overlay itself is still the active window.
        IntPtr foreground = GetForegroundWindow();
        uint foregroundThread = foreground == IntPtr.Zero
            ? 0
            : GetWindowThreadProcessId(foreground, out _);
        uint currentThread = GetCurrentThreadId();
        bool attached = foregroundThread != 0
            && foregroundThread != currentThread
            && AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            _ = SetForegroundWindow(game);
        }
        finally
        {
            if (attached)
            {
                _ = AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }

    private static IntPtr GameWindow()
    {
        try
        {
            foreach (Process process in Process.GetProcessesByName(GameProcessName))
            {
                using (process)
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        return process.MainWindowHandle;
                    }
                }
            }
        }
        // WHY: the process list races EVO starting/stopping; focus hand-over
        // is best-effort and the HUD click itself must never fail over it.
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
        }

        return IntPtr.Zero;
    }

    public static void SetClickThrough(IntPtr hWnd, bool clickThrough)
    {
        int style = GetWindowLong(hWnd, GWL_EXSTYLE);
        style = clickThrough
            ? style | WS_EX_TRANSPARENT
            : style & ~WS_EX_TRANSPARENT;
        _ = SetWindowLong(hWnd, GWL_EXSTYLE, style);
    }

    /// <summary>
    /// Keeps the HUD out of the game's focus while still letting the mouse
    /// drag it: clicks route to the window without activating it.
    /// </summary>
    public static void SetNoActivate(IntPtr hWnd)
    {
        int style = GetWindowLong(hWnd, GWL_EXSTYLE);
        _ = SetWindowLong(hWnd, GWL_EXSTYLE, style | WS_EX_NOACTIVATE);
    }
}
