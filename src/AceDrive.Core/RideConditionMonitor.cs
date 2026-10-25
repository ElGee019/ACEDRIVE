// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity's passive customer asks).
namespace AceDrive.Core;

/// <summary>
/// Watches an accepted passive ask's live limits for the paid leg. Pure and
/// sticky: the first broken rule is returned and kept, because the tip is gone
/// for good — the passenger never un-hears a redline. Feeding it is the host's
/// job (one call per poll).
///
/// The moving floor (> 15 km/h by default) keeps stops from counting against
/// the smoothness asks and the idle from counting against "keep the revs up".
/// The throttle ask is deliberately NOT gated: a full-throttle launch from the
/// door is exactly the brusque input those passengers complain about, and a
/// maximum-revs ask is broken by revving at a standstill too.
/// </summary>
public sealed class RideConditionMonitor
{
    private readonly RideCondition _condition;
    private readonly RideMonitorRules _rules;

    public RideConditionMonitor(RideCondition condition, RideMonitorRules rules)
    {
        _condition = condition;
        _rules = rules;
    }

    /// <summary>The promise being watched; the host formats its limits into
    /// the notice that explains the broken tip.</summary>
    public RideCondition Condition => _condition;

    /// <summary>The first rule broken, or None while the promise still holds.</summary>
    public RideViolation Violation { get; private set; }

    public RideViolation Observe(
        double speedKmh, int rpm, double gas, double brake, double steerRad)
    {
        if (Violation != RideViolation.None)
        {
            return Violation;
        }

        bool moving = speedKmh >= _rules.MovingKmh;
        if (_condition.MaxSpeedKmh is { } maxSpeed
            && speedKmh > maxSpeed + _rules.SpeedToleranceKmh)
        {
            Violation = RideViolation.Speed;
        }
        else if (_condition.MaxRpm is { } maxRpm && rpm > maxRpm)
        {
            Violation = RideViolation.RevsTooHigh;
        }
        else if (_condition.MinRpm is { } minRpm && moving && rpm < minRpm)
        {
            Violation = RideViolation.RevsTooLow;
        }
        else if (_condition.BrakeBrusque && moving && brake >= _rules.BrakeBrusque)
        {
            Violation = RideViolation.Brake;
        }
        else if (_condition.ThrottleBrusque && gas >= _rules.ThrottleBrusque)
        {
            Violation = RideViolation.Throttle;
        }
        else if (_condition.SteeringBrusque && moving
            && Math.Abs(steerRad) >= _rules.SteerBrusqueRad)
        {
            Violation = RideViolation.Steering;
        }

        return Violation;
    }
}
