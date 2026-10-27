// Pure gameplay data. No WPF / Windows / telemetry / backend references.
// The overlay turns these facts into the expansion page's copy; Core never
// carries a sentence (AGENTS.md › Data-driven).
namespace AceDrive.Core;

/// <summary>
/// What one operating-zone growth changed (Unity's FinishExpandCoroutine
/// report): the street that joined, the zone's new street count and area, and
/// how many jobs the next expansion still needs. A null growth result means
/// nothing joined, so no report exists.
/// </summary>
public sealed record ZoneExpansion(
    string StreetName,
    int StreetCount,
    double PreviousKm,
    double NewKm,
    int JobsToNext);
