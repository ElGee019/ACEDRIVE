// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 18 (Unity
// HubCareerCatalog/DeliveryHubScreen) + item 19 (escrow cart run).
namespace AceDrive.Core;

/// <summary>
/// The player's delivery career: the CredP balance (seeded by Max's intro
/// gift, PORTPLAN Career Start item 5) plus the hubs bought with it.
/// Unity: player.credP + player.ownedWholesaleHubs, in memory until the
/// save lands (PORTPLAN 51). Buying spends it; a delivered store earns it
/// back through <see cref="RegisterDelivery"/>'s ramp.
/// </summary>
public sealed class DeliveryCareer
{
    private readonly List<OwnedHub> _owned = [];
    // Per-hub delivery counts (Unity player.hubDeliveryStats), keyed by the
    // hub name — the same unit ownership is keyed on. Cold path: one entry
    // per delivered store, never inside a tick loop.
    private readonly Dictionary<string, int> _deliveries = new(StringComparer.OrdinalIgnoreCase);

    public DeliveryCareer(HubCatalog hubs, double startCredP)
    {
        Hubs = hubs;
        CredP = startCredP;
    }

    /// <summary>The purchasable content table this career spends against.</summary>
    public HubCatalog Hubs { get; }

    public double CredP { get; private set; }

    public IReadOnlyList<OwnedHub> Owned => _owned;

    // Unity DeliveryHubScreen.IsHubOwned: hubs are tracked by name across
    // the whole catalog, not per tier or category.
    public bool IsOwned(string hubName)
    {
        foreach (OwnedHub hub in _owned)
        {
            if (hub.Name == hubName) return true;
        }
        return false;
    }

    public bool OwnsCategory(string category)
    {
        foreach (OwnedHub hub in _owned)
        {
            if (hub.Category == category) return true;
        }
        return false;
    }

    /// <summary>Unity HubHelper.HasHubForCategory eligibility: a store can
    /// take orders only once a hub of its category is owned. A store with no
    /// category is open content that predates careers and always connects.</summary>
    public bool Connects(DeliveryStore store) =>
        store.Category.Length == 0 || OwnsCategory(store.Category);

    public IReadOnlyList<DeliveryStore> VisibleStores(DeliveryCatalog board)
    {
        var visible = new List<DeliveryStore>();
        foreach (DeliveryStore store in board.Stores)
        {
            if (Connects(store)) visible.Add(store);
        }
        return visible;
    }

    /// <summary>Unity DeliveryHubScreen.HasSelectedCareer: OPEN CAREER is
    /// enabled only while the pick exists on an enabled tier, is unowned
    /// and is affordable. There is no confirm dialog — this check is it.</summary>
    public bool CanBuy(int tierId, int careerIndex, int levelIndex) =>
        TryResolve(tierId, careerIndex, levelIndex, out _, out HubLevel? level) &&
        !IsOwned(level!.Name) && CredP >= level.Cost;

    /// <summary>Unity OnOpenCareerClicked: spend the CredP, add the hub and
    /// clear the selection. Category-less stores stay unconnected only until
    /// a hub of their category is bought — buying never rewires other careers.
    /// The owner person is assigned here like Unity does (Unity
    /// GetHubOwnerName at buy time), deterministically from the catalog's
    /// pool so the freeroam map can pin its POI to the same person later.
    /// Returns false (and changes nothing) when the pick is not buyable.</summary>
    public bool TryOpenCareer(int tierId, int careerIndex, int levelIndex)
    {
        if (!TryResolve(tierId, careerIndex, levelIndex, out HubCareer? career, out HubLevel? level))
        {
            return false;
        }
        if (IsOwned(level!.Name) || CredP < level.Cost) return false;

        // Unity numbers the owned level from 1 (its CredP ramp indexes by it).
        _owned.Add(new OwnedHub(
            level.Name, career!.Id, level.Cost, tierId, levelIndex + 1,
            HubOwnerNames.Pick(level.Name, Hubs.OwnersInForce)));
        CredP -= level.Cost;
        return true;
    }

    /// <summary>
    /// One delivered store's progression effect (Unity
    /// ApplyStoreDeliveryEffects): the producing hub earns the ramp rate
    /// through its per-hub delivery count and holds the run's friendship.
    /// When several hubs serve one category the newest purchase is the goods
    /// source the player upgraded to (the older hub merely connected the
    /// category). No owned hub means nothing earned and no owner: the
    /// caller hides the friendship block and skips the CredP line.
    /// Returns the owner to befriend and the CredP earned.
    /// </summary>
    public DeliveryEarning RegisterDelivery(string category)
    {
        if (NewestOwned(category) is not { } hub)
        {
            return new DeliveryEarning("", 0);
        }

        int before = _deliveries.GetValueOrDefault(hub.Name);
        int earned = Hubs.RampInForce.Earn(hub.Tier, hub.Level, before);
        _deliveries[hub.Name] = before + 1;
        CredP += earned;
        return new DeliveryEarning(hub.OwnerName, earned);
    }

    // The purchase that supplies the category is the newest one: buying a
    // further level of a career is the player's upgrade, so the goods source
    // moves with it (Unity leaves the old contract behind on the map).
    private OwnedHub? NewestOwned(string category)
    {
        for (int i = _owned.Count - 1; i >= 0; i--)
        {
            if (_owned[i].Category == category) return _owned[i];
        }
        return null;
    }

    /// <summary>The owner a run of this category would befriend, without moving
    /// any state: the same newest owned hub <see cref="RegisterDelivery"/>
    /// credits. Empty when no owned hub supplies the category. Read-only so a
    /// failed trip (a breakdown) can charge familiarity without crediting the
    /// ramp.</summary>
    public string OwnerFor(string category) => NewestOwned(category)?.OwnerName ?? "";

    private bool TryResolve(
        int tierId, int careerIndex, int levelIndex,
        out HubCareer? career, out HubLevel? level)
    {
        career = null;
        level = null;
        foreach (HubTier tier in Hubs.Tiers)
        {
            if (tier.Id != tierId) continue;
            // Unity only ever lists careers of enabled tiers as purchasable.
            if (!tier.Enabled) return false;
            if (careerIndex < 0 || careerIndex >= tier.Careers.Count) return false;
            HubCareer found = tier.Careers[careerIndex];
            if (levelIndex < 0 || levelIndex >= found.Levels.Count) return false;
            career = found;
            level = found.Levels[levelIndex];
            return true;
        }
        return false;
    }
}
