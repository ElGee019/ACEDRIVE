using System.Runtime.InteropServices;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The XInput pads: Xbox-compatible controllers, and wheels switched to XInput
/// mode, which Windows never exposes through raw input. XInput has no change
/// notification, so the only way to read a button is to ask — the poll lives
/// in <see cref="CallInput"/> and runs only while a call is being captured or
/// an XInput button is armed, never as a background loop.
/// </summary>
internal sealed class XInputPads
{
    private const int PadCount = 4;

    // Newest API first, then the older redistributables a game may have left
    // on the machine. The first one that loads is used for the session.
    private static readonly string[] Libraries =
    [
        "xinput1_4.dll",
        "xinput1_3.dll",
        "xinput9_1_0.dll",
    ];

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetState(uint index, out NativeInput.XInputState state);

    private readonly GetState? _getState;

    public XInputPads() => _getState = Resolve();

    /// <summary>False when no XInput runtime is installed: the poll is skipped.</summary>
    public bool IsAvailable => _getState is not null;

    /// <summary>The buttons held on any of the four pads, or 0 when none.</summary>
    public ushort ReadButtons()
    {
        if (_getState is null)
        {
            return 0;
        }

        ushort buttons = 0;
        for (uint index = 0; index < PadCount; index++)
        {
            if (_getState(index, out NativeInput.XInputState state) == 0)
            {
                buttons |= state.Gamepad.Buttons;
            }
        }

        return buttons;
    }

    private static GetState? Resolve()
    {
        foreach (string library in Libraries)
        {
            if (!NativeLibrary.TryLoad(library, out IntPtr module)
                || !NativeLibrary.TryGetExport(module, "XInputGetState", out IntPtr export))
            {
                continue;
            }

            return Marshal.GetDelegateForFunctionPointer<GetState>(export);
        }

        return null;
    }
}
