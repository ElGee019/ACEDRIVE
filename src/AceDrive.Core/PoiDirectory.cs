// The known points of interest behind the addresses page (PORTPLAN 24/27).
// Pure data: the story flow and the business spawner add doors, the page reads
// them back alphabetically, and a future marker network can use the same list.
namespace AceDrive.Core;

/// <summary>
/// The POIs the player has discovered so far, keyed by their stable id. An
/// address is banked the first time a door is added and never moves: a later
/// story beat or a newer data file cannot relocate a place the player has
/// already driven to. The list stays insertion-ordered; <see cref="Alphabetical"/>
/// is the page's view. A new career starts empty; the introduction and the
/// street/job thresholds fill it over time.
/// </summary>
public sealed class PoiDirectory
{
    private readonly Dictionary<string, PointOfInterest> _byId =
        new(StringComparer.Ordinal);
    private readonly List<PointOfInterest> _ordered = [];

    public PoiDirectory(IEnumerable<PointOfInterest>? known = null)
    {
        if (known is null)
        {
            return;
        }

        foreach (PointOfInterest poi in known)
        {
            Add(poi);
        }
    }

    /// <summary>Every known door in discovery order.</summary>
    public IReadOnlyList<PointOfInterest> All => _ordered;

    /// <summary>
    /// Adds a door once. False when the id is already known (the banked
    /// address wins) or the row is unusable, so a repeated discovery never
    /// rewrites a destination.
    /// </summary>
    public bool Add(PointOfInterest poi)
    {
        if (poi is null
            || string.IsNullOrWhiteSpace(poi.Id)
            || string.IsNullOrWhiteSpace(poi.Name)
            || string.IsNullOrWhiteSpace(poi.Address.StreetName)
            || poi.Address.HouseNumber < 1)
        {
            return false;
        }

        if (!_byId.TryAdd(poi.Id, poi))
        {
            return false;
        }

        _ordered.Add(poi);
        return true;
    }

    /// <summary>True when the id is already on the addresses page.</summary>
    public bool Contains(string? id) =>
        !string.IsNullOrWhiteSpace(id) && _byId.ContainsKey(id);

    /// <summary>
    /// The door with this stable id, or null when it is not known yet. The
    /// delivery pickup leg uses it to turn an owned hub into a destination.
    /// </summary>
    public PointOfInterest? Find(string? id) =>
        !string.IsNullOrWhiteSpace(id) && _byId.TryGetValue(id, out PointOfInterest? poi)
            ? poi
            : null;

    /// <summary>
    /// True when a known place already owns this door. A POI's owner lives or
    /// works there, so the resident draws must leave the house alone: one
    /// citizen per house, and this one is spoken for. Read live, never cached:
    /// the directory grows mid-session and a stale door set would hand a
    /// citizen the shop that just opened. The scan is a cold-path cost (a
    /// proposal or an offer, never the 10 Hz poll).
    /// </summary>
    public bool HasDoor(AddressLocation address)
    {
        foreach (PointOfInterest poi in _ordered)
        {
            if (poi.Address.Matches(address))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The page's view: every known door sorted by display name, then id, so
    /// two businesses with the same name still have a stable order.
    /// </summary>
    public IReadOnlyList<PointOfInterest> Alphabetical()
    {
        var sorted = new List<PointOfInterest>(_ordered);
        sorted.Sort(Compare);
        return sorted;
    }

    private static int Compare(PointOfInterest a, PointOfInterest b)
    {
        int byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        return byName != 0
            ? byName
            : string.Compare(a.Id, b.Id, StringComparison.Ordinal);
    }
}
