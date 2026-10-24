// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 18 (Unity
// HubCareerCatalog/DeliveryHubScreen) + item 19 (escrow cart run).
namespace AceDrive.Core;

/// <summary>
/// The player's delivery career: the CredP balance (starts empty; Max's
/// first store visit hands over the intro gift, PORTPLAN Career Start item 5)
/// plus the hubs bought with it. Unity: player.credP + player.ownedWholesaleHubs,
/// in memory until the save lands (PORTPLAN 51). Buying spends it; a delivered
/// store earns it back through <see cref="RegisterDelivery"/>'s ramp.
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

    /// <summary>
    /// Banks CredP earned outside the delivery ramp. Max's first-store visit
    /// uses it for the intro gift (Unity ScreenManager.OnMaxDialogueComplete:
    /// credP += 100 before the shop opens); the balance itself stays private so
    /// only gameplay rules move it.
    /// </summary>
    public void AddCredP(double amount)
    {
        if (amount <= 0) return;
        CredP += amount;
    }

    public IReadOnlyList<OwnedHub> Owned => _owned;

    /// <summary>
    /// A career that cannot buy anything yet: no CredP banked and no hub
    /// opened. Max's first store visit is the only way out (PORTPLAN Career
    /// Start item 5), so the delivery page points the player there.
    /// </summary>
    public bool NeedsIntroCredP => CredP <= 0 && _owned.Count == 0;

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
        // Key mirrors Unity's category_slug id so the hub's map door and the
        // owned record share one identity.
        _owned.Add(new OwnedHub(
            level.Name, career!.Id, level.Cost, tierId, levelIndex + 1,
            HubOwnerNames.Pick(level.Name, Hubs.OwnersInForce),
            HubKey(career.Id, level.Name)));
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
        if (OwnedFor(category) is not { } hub)
        {
            return new DeliveryEarning("", 0);
        }

        int before = _deliveries.GetValueOrDefault(hub.Name);
        int earned = Hubs.RampInForce.Earn(hub.Tier, hub.Level, before);
        _deliveries[hub.Name] = before + 1;
        CredP += earned;
        return new DeliveryEarning(hub.OwnerName, earned);
    }

    /// <summary>
    /// The newest hub that supplies the category, or null when none is owned.
    /// Buying a further level of a career is the player's upgrade, so the
    /// goods source moves with it (Unity leaves the old contract behind on
    /// the map). The delivery planner pins this hub's door for the pickup leg.
    /// </summary>
    public OwnedHub? OwnedFor(string category)
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
    public string OwnerFor(string category) => OwnedFor(category)?.OwnerName ?? "";

    // Unity's hub id: "<category>_<name with spaces as underscores>". The
    // same string becomes the POI id, so the pickup leg resolves the door by
    // the owned hub alone.
    private static string HubKey(string category, string name) =>
        $"{category}_{name.ToLowerInvariant().Replace(' ', '_')}";

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
