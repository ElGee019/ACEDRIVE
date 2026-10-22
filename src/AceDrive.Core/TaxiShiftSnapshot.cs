// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 12.
namespace AceDrive.Core;

/// <summary>
/// The resumable state of a taxi shift. The wall-clock stamps are what let a
/// shift outlive the process: the idle timeout is measured from the last
/// dropoff, so the same shift ends exactly 30 minutes after its latest job
/// whether or not the overlay was running in between.
/// </summary>
public sealed record TaxiShiftSnapshot(
    bool IsStarted,
    DateTime StartedAt,
    DateTime LastJobEnd,
    int JobsDone,
    double Earned);
