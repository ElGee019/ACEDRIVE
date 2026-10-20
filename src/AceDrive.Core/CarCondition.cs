// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN item 31, port of Unity
// Utils/CarMaintenanceService.cs — the per-part wear deck behind the mechanic
// page and the breakdown roll: every part wears with the odometer since its
// last service, the eight fractions average into one breakdown chance.
namespace AceDrive.Core;

/// <summary>
/// One car's maintenance deck (Unity OwnedCar.maintenanceParts): the odometer
/// each part was last serviced at. Wear is a pure function of (odometer,
/// baseline, interval), so the only state is the baseline per part. A part the
/// rules no longer list is ignored; a rule part missing from a saved row is
/// backfilled freshly serviced at the current odometer (Unity
/// EnsureInitialized, so legacy saves self-heal instead of reading 100% worn).
/// </summary>
public sealed class CarCondition
{
    private readonly MaintenanceRules _rules;
    private readonly Dictionary<string, double> _lastServiceKm = new(StringComparer.Ordinal);

    public CarCondition(MaintenanceRules rules, IReadOnlyList<MaintenancePartState>? saved, double odometer)
    {
        _rules = rules;
        // Seed every rule part at the current odometer: a brand-new car reads
        // 0% wear, and a legacy row without a part self-heals the same way.
        foreach (MaintenancePart part in rules.Parts)
        {
            _lastServiceKm[part.Key] = Math.Max(0.0, odometer);
        }
        if (saved is null)
        {
            return;
        }
        foreach (MaintenancePartState row in saved)
        {
            if (row is null)
            {
                continue;
            }
            string key = (row.Key ?? "").Trim();
            // A row the current rules do not know (a retired part) is dropped;
            // a non-finite or negative baseline is rejected, never trusted.
            if (key.Length == 0 || !_lastServiceKm.ContainsKey(key))
            {
                continue;
            }
            double last = row.LastServiceKm;
            if (double.IsFinite(last) && last >= 0.0)
            {
                _lastServiceKm[key] = last;
            }
        }
    }

    /// <summary>The parts the deck tracks, in rule/display order.</summary>
    public IReadOnlyList<MaintenancePart> Parts => _rules.Parts;

    /// <summary>Unity PartFraction: distance since service over the interval,
    /// clamped to 0..1. Unknown parts and zero intervals read 0.</summary>
    public double PartFraction(string key, double odometer)
    {
        if (!_lastServiceKm.TryGetValue(key, out double last))
        {
            return 0.0;
        }
        double interval = IntervalFor(key);
        if (interval <= 0.0)
        {
            return 0.0;
        }
        double since = Math.Max(0.0, odometer - last);
        return Math.Clamp(since / interval, 0.0, 1.0);
    }

    /// <summary>Unity's aggregate: the mean of every part's fraction.</summary>
    public double AverageWear(double odometer)
    {
        IReadOnlyList<MaintenancePart> parts = _rules.Parts;
        if (parts.Count == 0)
        {
            return 0.0;
        }
        double sum = 0.0;
        for (int i = 0; i < parts.Count; i++)
        {
            sum += PartFraction(parts[i].Key, odometer);
        }
        return sum / parts.Count;
    }

    /// <summary>Unity ComputeBreakdownChance: the average wear through the
    /// accelerating curve, capped at the rules' ceiling.</summary>
    public double BreakdownChance(double odometer)
    {
        double t = Math.Clamp(AverageWear(odometer), 0.0, 1.0);
        return Math.Clamp(_rules.MaxBreakdownChance * Math.Pow(t, _rules.CurveExponent), 0.0, 1.0);
    }

    /// <summary>Unity ServicePart: the part is replaced at the current odometer,
    /// so its fraction resets to 0. Unknown parts are ignored.</summary>
    public void ServicePart(string key, double odometer)
    {
        if (_lastServiceKm.ContainsKey(key))
        {
            _lastServiceKm[key] = Math.Max(0.0, odometer);
        }
    }

    /// <summary>Unity ReplaceEngine's part reset: a new engine zeroes every
    /// baseline so the displayed wear does not jump to 100% after the odometer
    /// resets to 0.</summary>
    public void ResetAll()
    {
        foreach (string key in _lastServiceKm.Keys)
        {
            _lastServiceKm[key] = 0.0;
        }
    }

    /// <summary>What a restart reopens, in rule order so the file stays stable.</summary>
    public IReadOnlyList<MaintenancePartState> Snapshot()
    {
        List<MaintenancePartState> rows = new(_rules.Parts.Count);
        foreach (MaintenancePart part in _rules.Parts)
        {
            rows.Add(new MaintenancePartState(part.Key, _lastServiceKm[part.Key]));
        }
        return rows;
    }

    private double IntervalFor(string key)
    {
        IReadOnlyList<MaintenancePart> parts = _rules.Parts;
        for (int i = 0; i < parts.Count; i++)
        {
            if (string.Equals(parts[i].Key, key, StringComparison.Ordinal))
            {
                return parts[i].IntervalKm;
            }
        }
        return 0.0;
    }
}
