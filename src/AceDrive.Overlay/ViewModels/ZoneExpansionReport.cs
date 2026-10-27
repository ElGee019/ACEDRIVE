using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The pending expansion hand-off: the Core facts of the street growth plus the
/// doors the same settle banked (TOZONES §4.5). The receipt's CONTINUE shows the
/// zone page before the page it would have opened; the report only exists when
/// a street actually joined, so a store-only or counter-only settle opens no
/// page.
/// </summary>
public sealed record ZoneExpansionReport(
    ZoneExpansion Zone,
    IReadOnlyList<PointOfInterest> Added);
