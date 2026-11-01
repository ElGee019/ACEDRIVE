namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One owned car on the insurance broker's cover list (Unity InsuranceScreen's
/// cloned row): the car, the ADD/REMOVE verb its cover earns, and whether the
/// row is the one whose price and premium the detail block shows. Preformatted
/// copy, so the view binds text only.
/// </summary>
public sealed record InsuranceCarRow(
    string Key,
    string Title,
    string ToggleLabel,
    bool IsInsured,
    bool IsSelected);
