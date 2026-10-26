// Cento Miglia club-reputation growth (PORTPLAN item 49, port of Unity
// MilleMigliaManager.ClubReputationTickerLoop + ReputationCalculator): Unity
// multiplies the club reputation by a small factor every tenth of a second
// while the race runs, so the port keeps the same factor but moves the measure
// from time to distance: the race charges one multiplication per 0.01 km the
// car actually covers, so a parked car (or an overlay left open on the start
// line) can no longer farm a club's standing. Pure arithmetic, so the companion
// can show the live value without owning a timer.
namespace AceDrive.Core;

/// <summary>
/// The race's reputation growth as a closed form over the driven distance:
/// Unity multiplied a float ten times a second, which the port charges once per
/// 0.01 km instead, so the rules stay a pure function of distance (the ticker
/// only banks it) and the shown number still climbs while the car moves.
/// </summary>
public static class CentoReputation
{
    /// <summary>
    /// The distance one growth tick is charged against: 10 m. Unity applied its
    /// factor every 0.1 s of racing; the port applies the same factor once per
    /// 0.01 km driven (PORTPLAN item 49), which is what stops a stationary car
    /// from building a reputation. A kilometre is therefore 100 ticks.
    /// </summary>
    public const double TickKilometres = 0.01;

    /// <summary>
    /// Unity ReputationCalculator.Divisor. The per-tick gain is tiny on
    /// purpose: (car class + owned cars) / 1,000,000, so a race adds a few
    /// thousandths, not a tier.
    /// </summary>
    public const double Divisor = 1_000_000.0;

    /// <summary>
    /// One tick's multiplier (Unity CalculateFactor). Unity also adds the
    /// driver's assertivity, a stat this port has not ported yet; the car
    /// class and the garage size are the whole factor here, so the port's
    /// growth matches Unity on the same inputs modulo that missing stat.
    /// Negative inputs are clamped instead of shrinking the reputation.
    /// </summary>
    public static double GrowthFactor(int carClass, int ownedCars)
    {
        double factor = 1.0
            + (Math.Max(0, carClass) / Divisor)
            + (Math.Max(0, ownedCars) / Divisor);
        return double.IsFinite(factor) && factor > 0.0 ? factor : 1.0;
    }

    /// <summary>
    /// The reputation after <paramref name="distanceKm"/> of racing: Unity's
    /// per-tick product, closed form, charged once per <see cref="TickKilometres"/>.
    /// An unusable start answers 0 the way the page floors it, and a
    /// non-positive or torn distance answers the start unchanged, so a bad
    /// sample never sends the counter backwards.
    /// </summary>
    public static double AfterDistance(
        double startReputation, int carClass, int ownedCars, double distanceKm)
    {
        if (!double.IsFinite(startReputation) || startReputation <= 0.0)
        {
            return 0.0;
        }

        if (!double.IsFinite(distanceKm) || distanceKm <= 0.0)
        {
            return startReputation;
        }

        double ticks = distanceKm / TickKilometres;
        double grown = startReputation
            * Math.Pow(GrowthFactor(carClass, ownedCars), ticks);
        return double.IsFinite(grown) && grown >= startReputation
            ? grown
            : startReputation;
    }
}
