using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The Touristenfahrten run's doors over the shared POI directory: existing
/// coffee shops and pizzerias are used as they are, and the run opens the shop
/// or corner it needs the first time, then banks it like any other discovered
/// door.
///
/// WHY the whole address map instead of the operating zone: the run is lap
/// catering around the Eifel circuit, so its corners may sit outside the zone
/// the career has discovered; the player can drive the whole lap either way.
/// The draw still skips every known door, so two places never share a house.
/// </summary>
public sealed class TouristenDoors : ITouristenDoors
{
    private readonly PoiDirectory _directory;
    private readonly IPoiDirectoryStore _store;
    private readonly PoiSpawner _spawner;
    private readonly AddressMap _map;
    private readonly TouristenMenu _menu;
    private readonly Random _rng;
    private readonly ILog _log;

    public TouristenDoors(
        PoiDirectory directory, IPoiDirectoryStore store, PoiSpawner spawner,
        AddressMap map, TouristenMenu menu, Random rng, ILog log)
    {
        _directory = directory;
        _store = store;
        _spawner = spawner;
        _map = map;
        _menu = menu;
        _rng = rng;
        _log = log;
    }

    public PointOfInterest? Pickup()
    {
        var known = new List<PointOfInterest>();
        foreach (PointOfInterest poi in _directory.All)
        {
            if (_menu.PickupFor(poi.Type) is not null)
            {
                known.Add(poi);
            }
        }

        if (known.Count > 0)
        {
            return known[_rng.Next(known.Count)];
        }

        if (TouristenRules.DrawPickup(_menu, _rng) is not { } kind)
        {
            _log.LogWarning("touristenfahrten pickup not placed: no pickup kind in the menu");
            return null;
        }

        string name = TouristenRules.PickupName(kind, _rng);
        return Place(
            UniqueId("touristen-pickup-" + kind.Kind), name, kind.PoiType, kind.Category);
    }

    public PointOfInterest? Dropoff(TouristenDropoffDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (_directory.Find(definition.Id) is { } known)
        {
            return known;
        }

        return Place(definition.Id, definition.Name, PoiTypes.TouristenDropoff, string.Empty);
    }

    // One free door on the map, banked with the directory it was drawn for.
    // A failed add (a content edit between the two calls) is not an error the
    // run can use: the stop is simply skipped.
    private PointOfInterest? Place(string id, string name, string type, string category)
    {
        PointOfInterest? poi = _spawner.Place(
            id, name, type, string.Empty, category, _directory, _map);
        if (poi is null || !_directory.Add(poi))
        {
            _log.LogWarning($"touristenfahrten door not placed: {name} ({id})");
            return null;
        }

        _store.Save(_directory.All);
        _log.LogInfo($"touristenfahrten door opened: {name} at " +
            AddressFormatter.Format(poi.Address));
        return poi;
    }

    // A hand-edited directory may already hold the family id (a pickup row
    // removed from the menu keeps its old door): the suffix keeps the door
    // unique instead of silently reusing an unrelated place.
    private string UniqueId(string stem)
    {
        string id = stem;
        int suffix = 1;
        while (_directory.Contains(id))
        {
            id = $"{stem}-{suffix}";
            suffix++;
        }

        return id;
    }
}
