// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Port of Unity's fuel pricing (GameClockManager.UpdateFuelPrice and
// FuelStationScreen): the money side only — EVO's own pump moves the fuel, so
// the overlay prices what the game already put in the tank.
namespace AceDrive.Core;

/// <summary>
/// The pump's balance table (PORTPLAN item 37): the starting CR/L, the daily
/// drift window, the clamp band, the owner a fill makes friends with, and the
/// two readings that tell a fill apart from a torn frame. Unity's shipped
/// numbers (PlayerDefaults.fuelPrice 1.65, GameTimeConfigSO −0.15..+0.15 and
/// 1.40..2.20) are the defaults; Data/fuel_rules.json overrides them.
/// </summary>
public sealed record FuelRules(
    double StartPricePerLitre,
    double DailyChangeMin,
    double DailyChangeMax,
    double MinPricePerLitre,
    double MaxPricePerLitre,
    double MinFillLitres,
    double SettleSeconds,
    double MinDriveBeforeFillMetres,
    string OwnerName)
{
    /// <summary>The test-shell fallback: Unity's shipped numbers and one
    /// station attendant who can still be named on a receipt.</summary>
    public static FuelRules Default { get; } = new(
        StartPricePerLitre: 1.65,
        DailyChangeMin: -0.15,
        DailyChangeMax: 0.15,
        MinPricePerLitre: 1.40,
        MaxPricePerLitre: 2.20,
        MinFillLitres: 0.5,
        SettleSeconds: 1.5,
        // WHY a drive gate: EVO tops the tank up on load/restart and at the pit
        // box, and the current freeroam map is a stand-in, so those top-ups
        // must never be priced. A fill only counts after the player has driven
        // at least this far from the spawn/session baseline.
        MinDriveBeforeFillMetres: 100.0,
        OwnerName: "Rolf Brenner");

    /// <summary>The same clamp Unity's UpdateFuelPrice applies after every
    /// daily change, rounded to the CR cent the rest of the ledger uses.</summary>
    public double Clamp(double pricePerLitre) =>
        Math.Round(
            Math.Clamp(pricePerLitre, MinPricePerLitre, MaxPricePerLitre),
            2,
            MidpointRounding.AwayFromZero);
}
