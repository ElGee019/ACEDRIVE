namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One owned car as the garage page's picker shows it: the preformatted title
/// and the flag that marks the car driven now. The title comes from the
/// catalogue row the car's key resolves, never from a copy taken at purchase
/// time. Picking a car only moves the stats panel; the drive action is the
/// page's separate button, so the current car is still selectable here.
/// </summary>
public sealed record GarageRow(
    string Key,
    string Title,
    bool IsCurrent);
