namespace AceDrive.Core;

/// <summary>
/// The one input that calls the panel, chosen on the setup page: a keyboard
/// chord, a wheel/DirectInput-pad button or an XInput pad button. The setup
/// page listens for the next press, stores what it got in
/// <see cref="PlayerPreferences.Call"/>, and the listener arms it so the panel
/// answers that input from the driver's seat.
///
/// <see cref="Code"/> is a virtual-key code for <see
/// cref="CallBindingKind.Keyboard"/> and a button usage/mask for the pad kinds;
/// <see cref="Modifiers"/> only carries a value for a chord. <see
/// cref="Label"/> is the platform's own name for the key or button ("C",
/// "CTRL+Shift+C", "A"), i.e. device nomenclature printed on the hardware, not
/// UI copy: the sentence around it lives in the resx. <see cref="Device"/> is
/// the HID identity a button belongs to (vendor/product), so the same button
/// on a second, identical device still matches; it is null for a keyboard
/// chord and for a pad button, which matches any pad.
/// </summary>
public sealed record CallBinding(
    CallBindingKind Kind,
    int Code,
    CallModifiers Modifiers,
    string Label,
    string? Device)
{
    // VK_C: the chord the window registered before the page could rebind it.
    private const int DefaultVirtualKey = 0x43;

    /// <summary>Ctrl+Shift+C: the call the app shipped with.</summary>
    public static readonly CallBinding Default = new(
        CallBindingKind.Keyboard,
        DefaultVirtualKey,
        CallModifiers.Control | CallModifiers.Shift,
        "CTRL+Shift+C",
        null);

    /// <summary>
    /// False for a save edited into nonsense (an unknown kind, no code, no
    /// label): the store then falls back to <see cref="Default"/> instead of
    /// arming a listener that can never fire.
    /// </summary>
    public bool IsUsable =>
        Enum.IsDefined(Kind)
        && Code > 0
        && !string.IsNullOrWhiteSpace(Label)
        && (Kind != CallBindingKind.Keyboard || Code <= 0xFF);

    /// <summary>
    /// True when <paramref name="other"/> is the same physical input. The label
    /// is not compared: it is the human name for the same chord/button, and a
    /// re-capture of it must still count as the same binding.
    /// </summary>
    public bool Matches(CallBinding other) =>
        Kind == other.Kind
        && Code == other.Code
        && Modifiers == other.Modifiers
        && string.Equals(Device, other.Device, StringComparison.OrdinalIgnoreCase);

    /// <summary>The binding, or the shipped chord when the save carries none.</summary>
    public static CallBinding OrDefault(CallBinding? binding) => binding ?? Default;
}
