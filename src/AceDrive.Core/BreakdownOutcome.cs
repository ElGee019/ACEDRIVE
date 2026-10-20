// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 39, port of Unity BreakdownFlow + BreakdownContext:
// one interrupted trip — its kind, the route fraction the car died at, the tow
// fee and whether the engine failed (the annual-inspection variant, which
// strands the car until the mechanic acts).
namespace AceDrive.Core;

/// <summary>
/// One breakdown outcome, handed to the page that reports it and to the host
/// that applies the per-kind consequences (taxi job cut, delivery cargo
/// stripped, free roam nothing). <see cref="Fraction"/> is Unity's 0.10-0.90
/// route point, 0.0 for the engine-failure variant (which fires at the start).
/// </summary>
public sealed record BreakdownOutcome(
    BreakdownKind Kind,
    double Fraction,
    double PickupFee,
    bool EngineFailure);
