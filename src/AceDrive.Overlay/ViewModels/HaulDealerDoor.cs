using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The car haul's pickup dealer over the shared POI directory: an already
/// discovered dealer is used as it is, and a career without one opens the
/// dealer its first haul rides from, banked like any other discovered door.
///
/// WHY on demand: Unity offers a haul from every dealership in the world, while
/// the port draws from the discovered directory only. Without this, a fresh
/// career is offered a job it cannot drive to — the dealer's own street/job
/// threshold sits at ten settled jobs or thirty-seven streets, which a player
/// looking for a haul has no reason to reach first. The opened dealer is a real
/// door: its shop serves the player like any other (dealerships are public in
/// Unity too).
/// </summary>
public sealed class HaulDealerDoor : IHaulDealerDoor
{
    private readonly PoiDirectory _directory;
    private readonly IPoiDirectoryStore _store;
    private readonly PoiSpawner _spawner;
    private readonly OperatingZone _zone;
    private readonly AddressMap _map;
    private readonly ILog _log;

    public HaulDealerDoor(
        PoiDirectory directory, IPoiDirectoryStore store, PoiSpawner spawner,
        OperatingZone zone, AddressMap map, ILog log)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(spawner);
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(log);

        _directory = directory;
        _store = store;
        _spawner = spawner;
        _zone = zone;
        _map = map;
        _log = log;
    }

    public PointOfInterest? Dealer()
    {
        if (KnownDealer() is { } known)
        {
            return known;
        }

        // The zone's discovered streets first, so the pickup sits where the
        // player already drives; a zone whose streets are full falls back to the
        // whole map, like the insurance broker's earned door, because a haul the
        // HUD has already promised must have somewhere to go.
        PointOfInterest? poi = _spawner.SpawnOnDemand(
                PoiTypes.Dealer, _directory, _zone.ZoneMap)
            ?? _spawner.SpawnOnDemand(PoiTypes.Dealer, _directory, _map);
        if (poi is null || !_directory.Add(poi))
        {
            _log.LogWarning("haul dealer not opened: no free house on the map");
            return null;
        }

        _store.Save(_directory.All);
        _log.LogInfo($"haul dealer opened: {poi.Name} at " +
            AddressFormatter.Format(poi.Address));
        return poi;
    }

    // The directory never banks a door without a street and house number, so
    // the only question here is whether the career has a dealer at all.
    private PointOfInterest? KnownDealer()
    {
        foreach (PointOfInterest poi in _directory.All)
        {
            if (string.Equals(poi.Type, PoiTypes.Dealer, StringComparison.OrdinalIgnoreCase))
            {
                return poi;
            }
        }

        return null;
    }
}
