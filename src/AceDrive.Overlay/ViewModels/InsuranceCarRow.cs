namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One owned car on the insurance broker's page (Unity InsuranceScreen's
/// cloned row): the car, its daily premium while covered and the ADD/REMOVE
/// toggle label. Preformatted copy, so the view binds text only.
/// </summary>
public sealed record InsuranceCarRow(
    string Key,
    string Title,
    string DailyLine,
    bool IsInsured,
    string ToggleLabel);
