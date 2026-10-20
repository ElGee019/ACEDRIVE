namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One owned car as the garage page shows it: preformatted title, spec line and
/// value, plus the flag the view paints. The specs come from the catalogue row the
/// car's key resolves, never from a copy taken at purchase time. The current car is
/// not a pick — Unity's CURRENT CAR row is non-interactable, and switching to the
/// car already driven is a no-op.
/// </summary>
public sealed record GarageRow(
    string Key,
    string Title,
    string SpecsLine,
    string ValueLine,
    bool IsCurrent)
{
    public bool IsSelectable => !IsCurrent;
}
