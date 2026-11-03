using System.Text;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The human names a call binding wears: the keyboard's own key names (from
/// the OS, in the layout the player uses) and the button numbers a HID device
/// reports. These are hardware nomenclature — the words printed on the key or
/// the button index the driver exposes — not UI copy, so the sentence around
/// them stays in the resx.
/// </summary>
internal static class InputNames
{
    // Virtual keys whose scan code needs the extended bit in GetKeyNameText's
    // lParam, or the OS answers with the numeric-pad key instead of the real
    // one (arrows, Home/End, the right-hand Ctrl/Alt).
    private static readonly int[] ExtendedKeys =
    [
        0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, // Prior, Next, End, Home, arrows
        0x2C, 0x2D, 0x2E, 0x5B, 0x5C, 0x5D, 0x6F, 0x90, // Snapshot, Insert, Delete, Win, Apps, Divide, Num
        0xA3, 0xA5, // right Ctrl, right Alt
    ];

    // XInput button codes are bit index + 1, so a driver update that renumbers
    // the masks cannot change what the save means.
    private static readonly string[] PadNames =
    [
        "D-PAD UP", "D-PAD DOWN", "D-PAD LEFT", "D-PAD RIGHT",
        "START", "BACK", "LS", "RS",
        "LB", "RB",
        "A", "B", "X", "Y",
    ];

    /// <summary>The chord text, e.g. "CTRL+Shift+C".</summary>
    public static string Chord(int virtualKey, CallModifiers modifiers)
    {
        var text = new StringBuilder(24);
        AppendModifiers(text, modifiers);
        text.Append(KeyName(virtualKey));
        return text.ToString();
    }

    /// <summary>The key's own name, e.g. "C", "F5", "Num 4".</summary>
    public static string KeyName(int virtualKey)
    {
        uint scanCode = NativeInput.MapVirtualKey((uint)virtualKey, 0);
        int nameParam = (int)(scanCode << 16);
        if (Array.IndexOf(ExtendedKeys, virtualKey) >= 0)
        {
            nameParam |= 1 << 24;
        }

        var name = new char[32];
        int written = NativeInput.GetKeyNameText(nameParam, name, name.Length);
        // Why the numeric fallback: a layout with no name for the key still
        // has to print something the player can recognise in the button.
        return written > 0 ? new string(name, 0, written) : $"Key {virtualKey}";
    }

    /// <summary>The HID button number the device reports, e.g. "Button 3".</summary>
    public static string HidButton(int usage) => $"Button {usage}";

    /// <summary>The XInput button a mask stands for, e.g. "A".</summary>
    public static string PadButton(int code) =>
        code >= 1 && code <= PadNames.Length ? PadNames[code - 1] : $"Button {code}";

    /// <summary>The lowest set button in a pad reading, as a stored code.</summary>
    public static int PadCode(ushort mask)
    {
        for (int bit = 0; bit < 16; bit++)
        {
            if ((mask & (1 << bit)) != 0)
            {
                return bit + 1;
            }
        }

        return 0;
    }

    /// <summary>The XInput mask a stored code stands for.</summary>
    public static ushort PadMask(int code) =>
        code is >= 1 and <= 16 ? (ushort)(1 << (code - 1)) : (ushort)0;

    private static void AppendModifiers(StringBuilder text, CallModifiers modifiers)
    {
        if (modifiers.HasFlag(CallModifiers.Control)) text.Append("CTRL+");
        if (modifiers.HasFlag(CallModifiers.Shift)) text.Append("Shift+");
        if (modifiers.HasFlag(CallModifiers.Alt)) text.Append("Alt+");
        if (modifiers.HasFlag(CallModifiers.Windows)) text.Append("Win+");
    }
}
