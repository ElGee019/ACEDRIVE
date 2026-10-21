// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11-13.
namespace AceDrive.Core;

/// <summary>Taxi job lifecycle phases (PORTPLAN item 11).</summary>
public enum JobPhase
{
    None,
    Proposed,   // offered, waiting for accept
    ToPickup,   // accepted, driving to the customer
    EnRoute,    // passenger aboard, meter running
    Complete,   // dropped off, receipt shown
}
