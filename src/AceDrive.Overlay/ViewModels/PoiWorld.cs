using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The discovered POI world (PORTPLAN items 24/27, Unity's business generators
/// plus the hub placement): the business doors the operating zone and the
/// settled-job count unlock, the shop doors each owned hub backfills, the hub
/// pickup doors, and the arrival handlers that route a known door into its
/// interior.
///
/// Owns the growth and the door state. The directory itself is shared with the
/// story flow (the introduction's errand doors) and the addresses page, so it
/// arrives as a store rather than being owned here; the shell only reports the
/// board refresh and the dealer's page outcome through
/// <see cref="IPoiWorldHost"/>. The calendar week the dealer's floor is drawn
/// from is the day cycle's value, read at the door.
/// </summary>
public sealed class PoiWorld
{
    private readonly PoiSpawner _growth;
    private readonly PoiDirectory _directory;
    private readonly IPoiDirectoryStore _store;
    private readonly DeliveryBoard _board;
    private readonly DeliveryCareer _career;
    private readonly CityEconomy _city;
    private readonly StoreCatalog _stores;
    private readonly OperatingZone _zone;
    private readonly JobLedger _jobs;
    private readonly IOnboardingStore _onboarding;
    private readonly ShopInteriors _shops;
    private readonly DayCycle _day;
    private readonly ILog _log;

    // WHY late: the host is the coordinator, which is built after this world.
    // The arrival delegates only read it when the player enters a door, which
    // is always after the attach in the coordinator's constructor.
    private IPoiWorldHost _host = null!;

    public PoiWorld(
        PoiSpawner growth, PoiDirectory directory, IPoiDirectoryStore store,
        DeliveryBoard board, DeliveryCareer career, CityEconomy city,
        StoreCatalog stores, OperatingZone zone, JobLedger jobs,
        IOnboardingStore onboarding, ShopInteriors shops, DayCycle day, ILog log)
    {
        _growth = growth;
        _directory = directory;
        _store = store;
        _board = board;
        _career = career;
        _city = city;
        _stores = stores;
        _zone = zone;
        _jobs = jobs;
        _onboarding = onboarding;
        _shops = shops;
        _day = day;
        _log = log;
    }

    /// <summary>Attaches the shell that owns the board page and the dealer page.</summary>
    public void Attach(IPoiWorldHost host) => _host = host;

    /// <summary>
    /// The business/store side of the marker network (Unity's CarWash/Bank/
    /// Insurance/Dealer/Mechanic generators plus the hub store backfill):
    /// every unlocked place opens on a free house on a street the zone already
    /// discovered. Cold path only (boot + settle + hub close), never the 10 Hz
    /// poll. The directory only grows, so a door the player already knows never
    /// moves. A fresh career is still in the introduction, and Unity's
    /// firstSceneComplete gate keeps the list empty until it ends.
    /// <paramref name="expansion"/> is the settle hook's growth report: its
    /// street receives the first new door (Unity's FinishExpandCoroutine), and
    /// the returned doors are the ones this call banked, so the expansion page
    /// can list the businesses that just opened. Stores are adopted into the
    /// board and the town before returning.
    /// </summary>
    public IReadOnlyList<PointOfInterest> Grow(ZoneExpansion? expansion = null)
    {
        if (!_onboarding.Load().IsComplete)
        {
            return [];
        }

        IReadOnlyList<PointOfInterest> grown = _growth.Spawn(
            _zone.StreetIds.Count, _jobs.Completed, _directory, _zone.ZoneMap,
            expansion?.StreetName, _career.OwnsCategory);
        var banked = new List<PointOfInterest>(grown.Count);
        foreach (PointOfInterest poi in grown)
        {
            if (!_directory.Add(poi))
            {
                continue;
            }

            banked.Add(poi);
            _log.LogInfo($"[Poi] opened {poi.Name} at " +
                AddressFormatter.Format(poi.Address));
        }

        if (banked.Count > 0)
        {
            _store.Save(_directory.All);
        }

        // A placed grocery/coffee store is also a delivery target: the shop
        // interior and the board row share the same POI identity, so the town
        // and the board gain the store together. Max's story grocery is the
        // career's first real grocery; adopting every known shop door also
        // covers the session where onboarding drew it after the catalog was
        // built at boot. This runs before the expansion report reaches the
        // page, so a shop listed there is already on the delivery board.
        if (AdoptDoors() || banked.Count > 0)
        {
            _host.RefreshBoard();
        }

        return banked;
    }

    /// <summary>
    /// Adds every known shop POI to the delivery board and the town, once.
    /// This is what makes the board list real stores instead of printed
    /// placeholder rows: Max's grocery plus the stores each owned hub has
    /// backfilled so far.
    /// </summary>
    public bool AdoptDoors()
    {
        bool changed = false;
        foreach (PointOfInterest poi in _directory.All)
        {
            if (PlacedStore.Row(poi, _stores) is not { } store)
            {
                continue;
            }

            bool newCell = _city.AddStore(store);
            if (_board.Catalog.Find(store.Key) is not null)
            {
                changed |= newCell;
                continue;
            }

            _board.Add(store);
            _log.LogInfo($"delivery board connected: {store.Name} " +
                $"({store.Category}) at {AddressFormatter.Format(poi.Address)}");
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// The arrival router's handler list: the root supplies the typed store
    /// handler, and one registration per business interior joins here. A new
    /// business is one entry beside the others, never a branch in the entry
    /// path (PORTPLAN item 24, OCP).
    /// </summary>
    public IReadOnlyList<IPoiArrivalHandler> Handlers(
        IReadOnlyList<IPoiArrivalHandler>? supplied = null)
    {
        var handlers = new List<IPoiArrivalHandler>();
        if (supplied is not null)
        {
            handlers.AddRange(supplied);
        }

        handlers.Add(new DelegatePoiHandler(
            PoiTypes.Bank, (_, back, changed) => _shops.Bank(back, changed)));
        handlers.Add(new DelegatePoiHandler(
            PoiTypes.Dealer, (_, back, _) =>
                _shops.Dealer(back, _day.DisplayWeek, _host.CarBought)));
        handlers.Add(new DelegatePoiHandler(
            PoiTypes.CarWash, (_, back, changed) => _shops.CarWash(back, changed)));
        handlers.Add(new DelegatePoiHandler(
            PoiTypes.Mechanic, (_, back, changed) => _shops.Mechanic(back, changed)));
        handlers.Add(new DelegatePoiHandler(
            PoiTypes.Insurance, (_, back, changed) => _shops.Insurance(back, changed)));
        return handlers;
    }

    /// <summary>
    /// The map side of one delivery stop: a hub's pickup door (placed at
    /// purchase) or the shop's own address. Null when the stop has no door left
    /// (a hand-edited catalogue), so the run can skip it instead of stalling.
    /// </summary>
    public AddressLocation? StopDoor(DeliveryStop leg) =>
        leg.Kind == DeliveryStopKind.Hub
            ? OwnedHubDoor(leg.Key)?.Address
            : _board.Catalog.Find(leg.Key)?.Address;

    /// <summary>
    /// The host's map side of a hub purchase: place its pickup door on a street
    /// the zone already discovered, before the backfilled stores draw their
    /// addresses, then bank the directory. A failed placement logs and leaves
    /// the pickup leg skipped; the rest of the run still works.
    /// </summary>
    public void PlaceHubDoor(OwnedHub hub) => _ = EnsureHubDoor(hub);

    private PointOfInterest? OwnedHubDoor(string key)
    {
        foreach (OwnedHub hub in _career.Owned)
        {
            if (hub.Key == key)
            {
                return EnsureHubDoor(hub);
            }
        }
        return null;
    }

    private PointOfInterest? EnsureHubDoor(OwnedHub hub)
    {
        if (_directory.Find(hub.Key) is { } known)
        {
            return known;
        }

        PointOfInterest? door = _growth.Place(
            hub.Key, hub.Name, PoiTypes.Hub, hub.OwnerName, hub.Category, _directory,
            _zone.ZoneMap);
        if (door is null || !_directory.Add(door))
        {
            _log.LogWarning($"hub door not placed: {hub.Name} ({hub.Key})");
            return null;
        }

        _store.Save(_directory.All);
        _log.LogInfo($"[Poi] opened hub {hub.Name} at " +
            AddressFormatter.Format(door.Address));
        return door;
    }
}
