using System.IO;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the pump's balance table from Data/fuel_rules.json next to the exe.
/// Missing or invalid content degrades to <see cref="FuelRules.Default"/>
/// (Unity's shipped numbers) with a warning — the overlay must never fail to
/// start over a data file, and a fill with no price is worse than a default.
/// </summary>
public sealed class JsonFuelRules
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonFuelRules(ILog log)
        : this(DataPaths.GameData("fuel_rules.json"), log)
    { }

    public JsonFuelRules(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public FuelRules Load()
    {
        if (!JsonTable.TryLoad(
                _log, _file, "fuel_rules.json", "using built-in defaults",
                IsValid, out FuelRules? rules))
        {
            return FuelRules.Default;
        }

        _log.LogInfo($"fuel rules loaded: CR {rules.StartPricePerLitre:0.00}/L start, " +
            $"band {rules.MinPricePerLitre:0.00}..{rules.MaxPricePerLitre:0.00}, " +
            $"drift {rules.DailyChangeMin:+0.00;-0.00}..{rules.DailyChangeMax:+0.00;-0.00}, " +
            $"roadside CR {rules.OutOfFuelServiceFee:0.00}/CR {rules.RoadServiceFee:0.00}");
        return rules;
    }

    // Every guard rejects a table that would misbehave at the pump: a band the
    // start price is outside, an inverted or empty drift window, or a fill
    // detector that can never settle. A malformed table is a data bug, so the
    // whole file falls back instead of loading half.
    private static bool IsValid(FuelRules rules) =>
        double.IsFinite(rules.StartPricePerLitre)
        && double.IsFinite(rules.MinPricePerLitre)
        && double.IsFinite(rules.MaxPricePerLitre)
        && double.IsFinite(rules.DailyChangeMin)
        && double.IsFinite(rules.DailyChangeMax)
        && rules.MinPricePerLitre > 0.0
        && rules.MinPricePerLitre < rules.MaxPricePerLitre
        && rules.StartPricePerLitre >= rules.MinPricePerLitre
        && rules.StartPricePerLitre <= rules.MaxPricePerLitre
        && rules.DailyChangeMin <= rules.DailyChangeMax
        && rules.MinFillLitres > 0.0
        && rules.SettleSeconds > 0.0
        && double.IsFinite(rules.MinDriveBeforeFillMetres)
        && rules.MinDriveBeforeFillMetres >= 0.0
        && double.IsFinite(rules.OutOfFuelServiceFee)
        && rules.OutOfFuelServiceFee >= 0.0
        && double.IsFinite(rules.RoadServiceFee)
        && rules.RoadServiceFee >= 0.0
        && rules.OwnerName is not null;
}
