// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: Unity CarHaulJob.status ("offered" | "picked_up" |
// "delivered") and the MapScreen arrival branches, PORTPLAN item 20.
namespace AceDrive.Core;

/// <summary>
/// The haul's three states. Unity keeps them as strings on the saved job; the
/// enum keeps the port's branches exhaustive and the save record maps to it.
/// </summary>
public enum CarHaulPhase
{
    /// <summary>Drawn and waiting at the dealer, not yet accepted (Unity's "offered").</summary>
    Offered,

    /// <summary>Picked up at the dealer, delivery leg armed (Unity's "picked_up").</summary>
    PickedUp,

    /// <summary>Reached the customer, settled (Unity's "delivered").</summary>
    Delivered,
}
