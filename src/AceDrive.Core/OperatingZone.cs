// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 58, Unity
// MapScreen.GetNextExpansionThreshold / ExpandOperatingZone. The discovery
// pipeline (OSM fetch, houses, markers) is DECLINED — the port grows the
// zone through the settle hooks; candidate streets arrive via the Step-1
// Eifel table, abstract ids carry the interim. Randomness is a seeded
// instance (AGENTS.md), never the shared global.
namespace AceDrive.Core;

/// <summary>
/// The operating-zone growth: every settled taxi or delivery job counts
/// toward the next expansion; reaching the needed jobs registers one more
/// street (Unity's expansion pipeline) and resets the counter. The bank's
/// interest rate reads the street COUNT, so the count must reach Unity
/// semantics before the bank port wires in.
/// </summary>
public sealed class OperatingZone
{
    /// <summary>The home street's interim id: Unity registers it when the
    /// Grandma onboarding dialogue completes, so a fresh driver answers day
    /// one with count 1. Until the Step-1 Eifel table names real streets,
    /// the placeholder keeps the count (and thus the interest factor)
    /// Unity-exact.</summary>
    public const string HomeStreetId = "home";

    private readonly Random _rng;
    private readonly List<string> _streets = [];
    private int _jobs;
    private int _expansions;
    private double _km;

    public OperatingZone(DriverState? saved, int seed)
    {
        _rng = new Random(seed);
        int jobs = Math.Max(0, saved?.ExpansionJobs ?? 0);
        int expansions = Math.Max(0, saved?.ExpansionCount ?? 0);
        _km = Math.Max(0.0, saved?.ZoneKm ?? 0.0);
        IReadOnlyList<string> savedStreets = saved?.StreetIds ?? [];
        if (savedStreets.Count == 0)
        {
            // Migration-free upgrade: an old save has no street list, so the
            // home street joins now (Unity: the registration already ran for
            // any driver past the Grandma dialogue).
            _streets.Add(HomeStreetId);
        }
        else
        {
            _streets.AddRange(savedStreets);
        }
        _jobs = jobs;
        _expansions = expansions;
    }

    /// <summary>Jobs counted toward the pending expansion (Unity
    /// expansionJobsCompleted).</summary>
    public int CountedJobs => _jobs;

    /// <summary>Expansions completed so far (Unity expansionCount); the next
    /// threshold curve reads this.</summary>
    public int ExpansionCount => _expansions;

    /// <summary>Discovered streets, oldest first (Unity discoveredStreets
    /// order; the home street leads).</summary>
    public IReadOnlyList<string> StreetIds => _streets;

    /// <summary>Total discovered street length in km (Unity
    /// currentOperatingZone); 0.0 until real street metadata lands.</summary>
    public double Km => _km;

    /// <summary>Jobs still missing before the next expansion (Unity HUD
    /// ExpansionCountText). Zero exactly when <see cref="ExpansionDue"/>.</summary>
    public int JobsUntilNext => Math.Max(0, Threshold(_expansions) - _jobs);

    /// <summary>True when the counters already reach the next expansion's
    /// needed jobs (Unity ExpandOperatingZone's first guard).</summary>
    public bool ExpansionDue => _jobs >= Threshold(_expansions);

    /// <summary>
    /// One settled taxi or delivery job bumps the counters (Unity
    /// expansionJobsCompleted++ at the two settle sites; car-haul never
    /// counts). Returns the new counter.
    /// </summary>
    public int RegisterJob()
    {
        _jobs++;
        return _jobs;
    }

    /// <summary>
    /// Registers the expansion when the counters reach the curve: one more
    /// street joins the zone (Unity's street registration), the job counter
    /// resets and the next threshold re-lotteries. The length lands on the
    /// zone radius; 0.0 is the current interim value until real street
    /// metadata arrives. Returns false when not due (no state change) or
    /// when the street is already in the zone.
    /// </summary>
    public bool TryExpand(string streetId, double lengthKm)
    {
        if (!ExpansionDue) return false;
        for (int i = 0; i < _streets.Count; i++)
        {
            if (string.Equals(_streets[i], streetId, StringComparison.OrdinalIgnoreCase))
            {
                // Unity's registry rejects double registration from the known
                // set; the caller re-picks a different street then.
                return false;
            }
        }
        _streets.Add(streetId);
        _km += Math.Max(0.0, lengthKm);
        _jobs = 0;
        _expansions++;
        return true;
    }

    /// <summary>Unity GetNextExpansionThreshold: 2 for the first expansion,
    /// 3 for the second, then Random.Range(4, 7) — 4 to 6 inclusive. The
    /// seeded instance re-draws per launch exactly like Unity's fresh
    /// Random recalculates on every MapScreen.Show.</summary>
    private int Threshold(int expansions) => expansions switch
    {
        0 => 2,
        1 => 3,
        _ => _rng.Next(4, 7),
    };
}
