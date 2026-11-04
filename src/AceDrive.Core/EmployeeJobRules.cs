// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: LATESTDRIVEBS2 EmployeeService (AssignRide / AssignDelivery):
// the town's employee work is not one shift length — a taxi job is a
// house-to-house ride timed by its drive plus a fixed handling block, while a
// delivery is a store refill that always takes exactly one day.
namespace AceDrive.Core;

/// <summary>
/// The balance numbers behind an employee job: how long each family keeps the
/// employee out and what it pays. Unity's own constants
/// (EmployeeService.MinRideLengthMin / DefaultHandlingMinutes /
/// DeliveryDurationHours / RefillEligibilityMaxFillPercent), kept in one place
/// so the assign service and the collect read the same rule.
/// </summary>
public static class EmployeeJobRules
{
    /// <summary>Employees drive at the shared car's assumed city speed.</summary>
    public const double AverageDriveSpeedKmH = CarShareRules.AverageDriveSpeedKmH;

    /// <summary>A house-to-house run never completes in its first minutes.</summary>
    public const int MinRideMinutes = 10;

    /// <summary>
    /// Handling on top of the drive time (pickup walk, paperwork, lunch): two
    /// hours by design, so an employee ride occupies a real chunk of the day.
    /// </summary>
    public const int HandlingMinutes = 120;

    /// <summary>
    /// A store refill always arrives exactly 24 hours after the click — the
    /// same clock time next day, so it always crosses midnight.
    /// </summary>
    public const int DeliveryMinutes = 24 * 60;

    /// <summary>
    /// Only stores at most this full are picked for a refill while one exists;
    /// when every store is fuller, any store with room qualifies. Unity's
    /// "prefer the stores that actually need stock" rule.
    /// </summary>
    public const double RefillEligibilityMaxFillPercent = 80.0;

    /// <summary>
    /// The friendship multiplier an employee ride pays. Unity's
    /// PlayerData.employeeFriendship is initialised to 1 and never raised by
    /// any gameplay path, so the ride pays its distance in CR; the formula is
    /// kept so a later friendship source changes this one number.
    /// </summary>
    public const double RideFriendship = 1.0;

    /// <summary>
    /// How long one employee ride takes: the distance at the assumed speed
    /// plus the handling block, and never under <see cref="MinRideMinutes"/>.
    /// </summary>
    public static int RideMinutes(double distanceKm)
    {
        double finite = double.IsFinite(distanceKm) && distanceKm > 0.0 ? distanceKm : 0.0;
        int driveMinutes = (int)Math.Round(finite / AverageDriveSpeedKmH * 60.0);
        return Math.Max(MinRideMinutes, driveMinutes + HandlingMinutes);
    }

    /// <summary>The CR one employee ride pays: distance × the friendship factor.</summary>
    public static double RidePay(double distanceKm) =>
        double.IsFinite(distanceKm) && distanceKm > 0.0
            ? distanceKm * RideFriendship
            : 0.0;

    /// <summary>True while the store has shelf space an employee refill can fill.</summary>
    public static bool CanRefill(DeliveryStore store) =>
        store is not null && store.UnitsNeeded > 0;

    /// <summary>True while the store is at most 80 % full: the preferred refill targets.</summary>
    public static bool IsRefillPreferred(DeliveryStore store) =>
        CanRefill(store) && store.StockPercent <= RefillEligibilityMaxFillPercent;

    /// <summary>
    /// What a completed refill pays: one CR per unit added to the shelves.
    /// Unity's EmployeeService credits the added unit count, so a fuller store
    /// is worth a bigger run.
    /// </summary>
    public static double DeliveryPay(int unitsAdded) => Math.Max(0, unitsAdded);
}
