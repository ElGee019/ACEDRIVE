namespace AceDrive.Core;

/// <summary>
/// The modifiers a keyboard call holds down. Numbered exactly like the Win32
/// MOD_* flags on purpose: the listener hands the value straight to
/// RegisterHotKey, so no translation step can drift from the OS.
/// </summary>
[Flags]
public enum CallModifiers
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
}
