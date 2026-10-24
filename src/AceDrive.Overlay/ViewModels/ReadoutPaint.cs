namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The readout strings one tick or one job change flipped, produced by
/// <see cref="TelemetryReadouts"/> and copied onto the coordinator's bindable
/// properties. A null member means "leave the painted value alone", so a
/// parked car costs no string work and no notification per tick (AGENTS.md
/// hot-path discipline).
/// </summary>
public readonly record struct ReadoutPaint(
    bool? Live = null,
    string? Status = null,
    string? Speed = null,
    string? Gear = null,
    string? Detail = null,
    string? Job = null,
    string? Fare = null,
    string? Shift = null);
