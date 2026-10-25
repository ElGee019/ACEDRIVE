// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity's passive customer asks).
namespace AceDrive.Core;

/// <summary>
/// The thresholds behind the passive asks' live limits. Shipped values live in
/// Data/customer_requests.json's "monitor" block; this default is the
/// test-shell fallback and the port's tuning baseline (brake pedal 70 %,
/// throttle 80 %, 0.35 rad of steering, limits read with a 1 km/h tolerance).
/// </summary>
public sealed record RideMonitorRules(
    double MovingKmh = 15.0,
    double BrakeBrusque = 0.70,
    double ThrottleBrusque = 0.80,
    double SteerBrusqueRad = 0.35,
    double SpeedToleranceKmh = 1.0)
{
    public static readonly RideMonitorRules Default = new();

    public bool IsValid =>
        double.IsFinite(MovingKmh) && MovingKmh is >= 0.0 and < 200.0
        && double.IsFinite(BrakeBrusque) && BrakeBrusque is > 0.0 and <= 1.0
        && double.IsFinite(ThrottleBrusque) && ThrottleBrusque is > 0.0 and <= 1.0
        && double.IsFinite(SteerBrusqueRad) && SteerBrusqueRad is > 0.0 and < Math.PI
        && double.IsFinite(SpeedToleranceKmh) && SpeedToleranceKmh is >= 0.0 and < 20.0;
}
