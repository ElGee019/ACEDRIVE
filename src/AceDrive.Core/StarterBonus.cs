// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 47 (Unity
// ProvincialStarterBonus): the early-career earnings boost every taxi payout
// carries while the operating zone is still small.
namespace AceDrive.Core;

/// <summary>
/// The business starter bonus: an early-game earnings boost that decays with
/// the operating zone and the number of owned cars. The zone part pays 100%
/// under 1 km, 99% under 2 km, 98% under 3 km and so on — 0% once the zone
/// reaches 100 km. The car part divides that percentage by the owned car count
/// (1 car keeps it whole, 2 cars halve it, ...), so a growing business earns
/// less starter support per ride.
/// </summary>
public static class StarterBonus
{
    /// <summary>Zone length where the boost has fully decayed.</summary>
    public const int MaxZoneKm = 100;

    /// <summary>Zone-only percentage (0–100): 100 under 1 km, … 0 at 100 km+.</summary>
    public static double ZonePercentage(double zoneKm)
    {
        // A save can carry a negative or non-finite length; both mean "no
        // zone yet" rather than a percentage outside the 0-100 band.
        double km = double.IsFinite(zoneKm) && zoneKm > 0.0 ? zoneKm : 0.0;
        return Math.Max(0.0, MaxZoneKm - Math.Floor(km));
    }

    /// <summary>Zone percentage divided by the owned car count (min 1).</summary>
    public static double Percentage(double zoneKm, int ownedCars) =>
        ZonePercentage(zoneKm) / Math.Max(1, ownedCars);

    /// <summary>
    /// The CR added on top of the pre-bonus earnings. A net-negative ride
    /// (fines above fare and tip) earns no boost: unlike Unity's raw float
    /// multiply, a "bonus" must never take more money away.
    /// </summary>
    public static double Amount(double baseAmount, double zoneKm, int ownedCars)
    {
        double amount = double.IsFinite(baseAmount) && baseAmount > 0.0 ? baseAmount : 0.0;
        return amount * Percentage(zoneKm, ownedCars) / 100.0;
    }
}
