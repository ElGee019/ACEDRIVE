using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AceDrive.Overlay.Services;

/// <summary>Win32 interop: click-through toggle, focus hand-over, window styles.</summary>
internal static class Native
{
    private const string GameProcessName = "AssettoCorsaEVO";

    public const int GWL_EXSTYLE = -20;
    public const int GWL_STYLE = -16;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_CAPTION = 0x00C00000;
    public const int WS_THICKFRAME = 0x00040000;
    public const int WM_ENTERSIZEMOVE = 0x0231;
    public const int WM_EXITSIZEMOVE = 0x0232;
    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int WM_NCHITTEST = 0x0084;
    public const int MA_NOACTIVATE = 3;
    // Hit-test results returned from WM_NCHITTEST to keep the resize grip alive
    // on a window whose WS_THICKFRAME was cleared (a borderless window with no
    // frame region for the DWM to paint white). BOTTOMRIGHT restores the grip.
    public const int HTBOTTOMRIGHT = 17;
    public const int HTBOTTOM = 15;
    public const int HTRIGHT = 11;

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(IntPtr hWnd, ref Point point);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// Converts a screen point to the window's client coordinates for the
    /// WM_NCHITTEST resize grip. False when the conversion fails, so the caller
    /// falls back to the default hit-test instead of misreporting an edge.
    /// </summary>
    public static bool TryScreenToClient(IntPtr hWnd, int x, int y, out int clientX, out int clientY)
    {
        var point = new Point { X = x, Y = y };
        if (ScreenToClient(hWnd, ref point))
        {
            clientX = point.X;
            clientY = point.Y;
            return true;
        }

        clientX = 0;
        clientY = 0;
        return false;
    }

    // The DWM corner preference: 0x21 sets DWMWA_WINDOW_CORNER_PREFERENCE and
    // 2 is DWMWCP_ROUND. This replaces the layered-window rounded corners that
    // AllowsTransparency used to fake, so the overlay can render opaque
    // (hardware-accelerated) and still keep its chrome. Win11 only: older
    // builds ignore the attribute, so the window simply keeps square corners.
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hWnd, int attribute, ref int value, int valueSize);

    /// <summary>
    /// Rounds the window's corners through the DWM instead of a layered window.
    /// Best-effort: the attribute is ignored before Win11 (the call returns
    /// E_INVALIDARG), leaving square corners rather than failing the window.
    /// </summary>
    public static void RoundCorners(IntPtr hWnd)
    {
        int preference = DwmwcpRound;
        _ = DwmSetWindowAttribute(
            hWnd, DwmwaWindowCornerPreference, ref preference, sizeof(int));
    }

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

    /// <summary>
    /// Clears the caption and thick-frame styles so an opaque borderless window
    /// has no non-client region for the DWM to paint. On Win11 a
    /// WindowStyle=None window still carrying WS_THICKFRAME (from
    /// ResizeMode=CanResizeWithGrip) draws a white strip along its top edge,
    /// which the DWM's rounded corner then cuts into. The grip's resize survives
    /// through WM_NCHITTEST (see MainWindow.WndProc) instead of the frame.
    /// </summary>
    public static void SetBorderless(IntPtr hWnd)
    {
        int style = GetWindowLong(hWnd, GWL_STYLE);
        _ = SetWindowLong(hWnd, GWL_STYLE, style & ~(WS_CAPTION | WS_THICKFRAME));
    }
}
