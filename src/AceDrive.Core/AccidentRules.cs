// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The ACE accident repair economy (Unity RepairCostCalculator's successor):
// the game publishes collision damage, the overlay prices the repair off the
// speed the car carried into the hit relative to its own top speed — a top
// speed impact writes off the whole car, half the top speed half the value,
// and so on linearly. Unity scaled by trip distance instead; this port scales
// by what the physics block actually reports.
namespace AceDrive.Core;

public static class AccidentRules
{
    /// <summary>
    /// The repair bill as a fraction of the car's value, 0..1. The impact
    /// speed is divided by the car's rated top speed, so the same 50 km/h hit
    /// costs a 150 km/h city car a third of its value and a 300 km/h sports car
    /// a sixth. A missing or non-positive top speed reads 0 (the caller logs
    /// the gap rather than inventing a denominator); an impact above the rated
    /// top speed clamps to a total write-off.
    /// </summary>
    public static double RepairFraction(double impactSpeedKmh, double topSpeedKmh)
    {
        if (!double.IsFinite(impactSpeedKmh)
            || !double.IsFinite(topSpeedKmh)
            || topSpeedKmh <= 0.0)
        {
            return 0.0;
        }
        return Math.Clamp(impactSpeedKmh / topSpeedKmh, 0.0, 1.0);
    }

    /// <summary>The CR a repair bills for one impact: the car's value times
    /// <see cref="RepairFraction"/>.</summary>
    public static double RepairCost(double carValue, double impactSpeedKmh, double topSpeedKmh)
    {
        if (!double.IsFinite(carValue) || carValue <= 0.0)
        {
            return 0.0;
        }
        return carValue * RepairFraction(impactSpeedKmh, topSpeedKmh);
    }
}
