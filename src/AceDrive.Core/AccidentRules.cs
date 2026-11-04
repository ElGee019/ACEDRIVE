// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The ACE accident repair economy (Unity RepairCostCalculator's successor):
// the game publishes collision damage, the overlay prices the repair off the
// speed the car carried into the hit relative to its own top speed. Unity
// scaled by trip distance instead; this port scales by what the physics block
// actually reports.
namespace AceDrive.Core;

public static class AccidentRules
{
    /// <summary>
    /// HOW much of the raw speed share a hit actually costs. WHY a twentieth:
    /// the raw share wrote off half a 40 000 CR car for one 120 km/h shunt, and
    /// even a tenth still billed 2 000 CR for that one bump — twenty times the
    /// 100 CR a career starts with, which reads as a tax on existing rather
    /// than the price of one mistake. A full-speed impact now costs 5 % of the
    /// car and it takes twenty of them, unrepaired, to total it.
    /// </summary>
    public const double RepairScale = 0.05;

    /// <summary>
    /// The repair bill as a fraction of the car's value, 0..1. The impact
    /// speed is divided by the car's rated top speed, so the same 50 km/h hit
    /// costs a 150 km/h city car a third as much as a 300 km/h sports car; the
    /// share is then scaled by <see cref="RepairScale"/>. A missing or
    /// non-positive top speed reads 0 (the caller logs the gap rather than
    /// inventing a denominator); an impact above the rated top speed clamps to
    /// a twentieth of the value, the same as a hit at exactly the rated speed.
    /// </summary>
    public static double RepairFraction(double impactSpeedKmh, double topSpeedKmh)
    {
        if (!double.IsFinite(impactSpeedKmh)
            || !double.IsFinite(topSpeedKmh)
            || topSpeedKmh <= 0.0)
        {
            return 0.0;
        }
        return Math.Clamp(impactSpeedKmh / topSpeedKmh, 0.0, 1.0) * RepairScale;
    }

    /// <summary>
    /// The CR a share of the car's value bills. The caller passes the share the
    /// car actually took (<see cref="OwnedCar.AddBodyDamage"/> returns it, so a
    /// car already written off is billed nothing more), and the clamp here is
    /// the last line of defence: several crashes accumulate, but their repair
    /// can never cost more than the car itself.
    /// </summary>
    public static double RepairCost(double carValue, double fraction)
    {
        if (!double.IsFinite(carValue) || carValue <= 0.0 || !double.IsFinite(fraction))
        {
            return 0.0;
        }
        return carValue * Math.Clamp(fraction, 0.0, 1.0);
    }
}
