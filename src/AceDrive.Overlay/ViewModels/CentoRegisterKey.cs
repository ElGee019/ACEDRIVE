namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One keypad digit on the registration page: the number the button prints and
/// whether that digit is part of the number selected so far. The page rebuilds
/// the list after every tap and the view paints a selected key teal, so the
/// player reads the number they picked straight off the keypad (Unity's
/// RegisterScreen has no such mark; the port adds it because the digits are
/// their own control here).
/// </summary>
public sealed record CentoRegisterKey(int Digit, bool Selected);
