// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 39, port of Unity Driving/Services/
// BreakdownJobKind: which kind of trip was interrupted, so the consequence and
// the wording follow (taxi ride, delivery run, free roam).
namespace AceDrive.Core;

/// <summary>The trip a breakdown interrupted (Unity BreakdownJobKind).</summary>
public enum BreakdownKind
{
    Taxi,
    Delivery,
    Freeroam,
}
