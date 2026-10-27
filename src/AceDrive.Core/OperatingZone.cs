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
///
/// The zone is also the owner of the addressable area: <see cref="ZoneMap"/>
/// names the streets it covers (home first, then map order) and
/// <see cref="NextStreet"/> is the row the next expansion would add. Draws
/// that must stay inside the discovered pool read <see cref="ZoneMap"/>
/// instead of the whole map.
/// </summary>
public sealed class OperatingZone
{
    /// <summary>The home street's interim id: Unity registers it when the
    /// Grandma onboarding dialogue completes, so a fresh driver answers day
    /// one with count 1. Until the Step-1 Eifel table names real streets,
    /// the placeholder keeps the count (and thus the interest factor)
    /// Unity-exact, and only the count is ever read back from a save.</summary>
    public const string HomeStreetId = "home";

    private readonly Random _rng;
    private readonly AddressMap _map;
    // Home street first, then map order: one cached order feeds both the
    // discovered prefix (ZoneMap) and the row after it (NextStreet), so the
    // two lookups can never disagree.
    private readonly IReadOnlyList<AddressStreet> _orderedStreets;
    private readonly List<string> _streets = [];
    private AddressMap _zoneMap;
    private int _jobs;
    private int _expansions;
    private double _km;

    public OperatingZone(
        DriverState? saved, int seed, AddressMap map,
        string? homeStreetId = null, double initialKm = 0.0)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
        _orderedStreets = AddressBook.ZoneStreets(map, int.MaxValue, homeStreetId);
        _rng = new Random(seed);
        int jobs = Math.Max(0, saved?.ExpansionJobs ?? 0);
        int expansions = Math.Max(0, saved?.ExpansionCount ?? 0);
        // A saved length always wins. A fresh (or pre-street-metadata) save
        // seeds from the placeholder map instead of 0 km, so the starter
        // bonus and the zone board start from the interim free-roam area.
        _km = Math.Max(0.0, saved?.ZoneKm ?? 0.0);
        if (_km <= 0.0)
        {
            _km = Math.Max(0.0, initialKm);
        }
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
        _zoneMap = BuildZoneMap();
    }

    /// <summary>Jobs counted toward the pending expansion (Unity
    /// expansionJobsCompleted).</summary>
    public int CountedJobs => _jobs;

    /// <summary>Expansions completed so far (Unity expansionCount); the next
    /// threshold curve reads this.</summary>
    public int ExpansionCount => _expansions;

    /// <summary>Discovered streets, oldest first (Unity discoveredStreets
    /// order; the home street leads). Only the COUNT is meaningful: legacy
    /// saves carry the interim ids ("home", "s1", …), while the map names the
    /// streets through <see cref="ZoneMap"/>.</summary>
    public IReadOnlyList<string> StreetIds => _streets;

    /// <summary>The discovered streets as an address map, home street first.
    /// Every new draw (taxi proposal, venue, generated business, Willy) reads
    /// this so nothing can land on a street the zone has not reached. Rebuilt
    /// by <see cref="TryExpand"/> — the cache's only writer.</summary>
    public AddressMap ZoneMap => _zoneMap;

    /// <summary>The street the next expansion would add, or null once the map's
    /// pool is exhausted. Read from the cached home-first order at the zone's
    /// street count, so a legacy save needs no migration.</summary>
    public AddressStreet? NextStreet =>
        _streets.Count < _orderedStreets.Count
            ? _orderedStreets[_streets.Count]
            : null;

    /// <summary>Total discovered street length in km (Unity
    /// currentOperatingZone); seeded from the placeholder map until real
    /// street metadata lands.</summary>
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
        _zoneMap = BuildZoneMap();
        return true;
    }

    // The discovered streets are the prefix of the cached home-first order:
    // the count is the save's authority, the map supplies the identities.
    private AddressMap BuildZoneMap()
    {
        int count = Math.Min(_streets.Count, _orderedStreets.Count);
        var streets = new List<AddressStreet>(count);
        for (int i = 0; i < count; i++)
        {
            streets.Add(_orderedStreets[i]);
        }
        return _map with { Streets = streets };
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
