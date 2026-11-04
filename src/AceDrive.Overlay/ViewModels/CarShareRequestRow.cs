namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One car-sharing request as the assign page paints it: the leave time, the
/// customer, the pickup → drop-off line and the ride's distance, all already
/// formatted with the resx shapes. The page only lays the four fields out, so
/// the row never re-derives a number or a unit. The ride's XP is deliberately
/// not shown: the block states the earning rule, and the collect banks the
/// batch total without the player adding rows up.
/// </summary>
public sealed record CarShareRequestRow(
    string Time, string Customer, string Route, string Distance);
