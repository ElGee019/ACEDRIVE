namespace AceDrive.Core;

/// <summary>
/// Which stream a call binding's press arrives on: a keyboard chord, a HID
/// button (a wheel or a DirectInput pad) or an XInput pad button. The listener
/// picks its watcher from this: the OS hotkey for a chord, the raw-input
/// stream for a HID button, the pad poll for a pad. Values are explicit so a
/// save written by an older build keeps its meaning.
/// </summary>
public enum CallBindingKind
{
    Keyboard = 0,
    HidButton = 1,
    PadButton = 2,
}
