// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
namespace AceDrive.Core;

/// <summary>
/// Turns EVO's cumulative collision damage into impact events. WHY this shape:
/// the physics block has no collision flag, but it does carry a persistent
/// damage counter per zone, so a crash is a positive jump between two samples
/// rather than a transient the 10 Hz poll could miss. The speed reported is the
/// one from the sample <em>before</em> the jump — by the time the poll sees the
/// damage the car has usually already stopped, which would price every crash at
/// zero.
///
/// A single collision can land across two ticks (the counter climbs once per
/// physics step), so the detector latches a short cooldown and folds the second
/// jump into the same event; a repair or session reload drops the counter and
/// is silently re-baselined, never billed.
/// </summary>
public sealed class AccidentDetector
{
    /// <summary>WHY so small: EVO writes absolute damage units (a verified
    /// front+centre crash read ~199/237), so any visible jump is a real hit.
    /// The floor only rejects float noise on an untouched counter.</summary>
    public const double DamageEpsilon = 0.01;

    /// <summary>One crash, one event: the physics counter can rise on two
    /// consecutive 10 Hz samples while the contact settles.</summary>
    public const double ImpactCooldownSeconds = 1.5;

    private bool _primed;
    private double _lastDamage;
    private double _lastSpeed;
    private DateTime _cooldownUntil = DateTime.MinValue;

    /// <summary>
    /// Feeds one poll. Null while nothing happened, during the settle window,
    /// and on a torn read (a non-finite sample re-primes the detector instead
    /// of reporting a crash against garbage).
    /// </summary>
    public AccidentImpact? Observe(double damageTotal, double speedKmh, DateTime now)
    {
        if (!double.IsFinite(damageTotal) || !double.IsFinite(speedKmh))
        {
            // WHY re-prime: after a torn read the next good sample becomes the
            // baseline, so the gap cannot surface as a phantom impact.
            _primed = false;
            return null;
        }
        if (!_primed)
        {
            _primed = true;
            _lastDamage = damageTotal;
            _lastSpeed = speedKmh;
            return null;
        }

        // The pre-impact speed is the one captured last time: the car may
        // already be stopped by this sample.
        double previousSpeed = _lastSpeed;
        double delta = damageTotal - _lastDamage;
        _lastDamage = damageTotal;
        _lastSpeed = speedKmh;

        if (delta < DamageEpsilon || now < _cooldownUntil)
        {
            return null;
        }

        _cooldownUntil = now.AddSeconds(ImpactCooldownSeconds);
        return new AccidentImpact(previousSpeed, delta);
    }
}
