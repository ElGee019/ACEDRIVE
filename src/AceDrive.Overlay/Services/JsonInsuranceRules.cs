using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the insurance rules from Data/insurance_rules.json next to the exe.
/// Missing or invalid content degrades to <see cref="InsuranceRules.Default"/>
/// (Unity's shipped config) with a warning — the overlay must never fail to
/// start over a data file.
/// </summary>
public sealed class JsonInsuranceRules
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonInsuranceRules(ILog log)
        : this(DataPaths.GameData("insurance_rules.json"), log)
    { }

    public JsonInsuranceRules(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public InsuranceRules Load()
    {
        if (!JsonTable.TryLoad(
                _log, _file, "insurance_rules.json", "using built-in defaults",
                IsValid, out InsuranceRules? rules))
        {
            return InsuranceRules.Default;
        }

        _log.LogInfo($"insurance rules loaded: rate {rules.PremiumRate}, " +
            $"+{(rules.BaseIncreaseFactor - 1.0) * 100.0:0}%, floor CR{rules.MinimumDailyCost:0}");
        return rules;
    }

    // A free or negative rate would hand out free cover; an increase below 1
    // would make the "general +15%" copy a lie; a floor below 0 or an hour
    // outside the day can never be charged.
    private static bool IsValid(InsuranceRules rules) =>
        rules.PremiumRate > 0.0
        && rules.BaseIncreaseFactor >= 1.0
        && rules.MinimumDailyCost >= 0.0
        && rules.ChargeHour is >= 0 and <= 23;
}
