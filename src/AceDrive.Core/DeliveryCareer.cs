// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 18 (wholesale
// hub ownership + credibility/CredP ramp), port of Unity DeliveryHubScreen
// + StoreListView eligibility rules.
namespace AceDrive.Core;

/// <summary>
/// The player's delivery career: the CredP balance (seeded by Max's intro
/// gift, PORTPLAN Career Start item 5) plus the hubs bought with it.
/// Unity: player.credP + player.ownedWholesaleHubs, in memory until the
/// save lands (PORTPLAN 51). Earning CredP is the pickup run's job; this
/// class only ever spends it.
/// </summary>
public sealed class DeliveryCareer
{
    private readonly List<OwnedHub> _owned = [];

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
    /// Returns false (and changes nothing) when the pick is not buyable.</summary>
    public bool TryOpenCareer(int tierId, int careerIndex, int levelIndex)
    {
        if (!TryResolve(tierId, careerIndex, levelIndex, out HubCareer? career, out HubLevel? level))
        {
            return false;
        }
        if (IsOwned(level!.Name) || CredP < level.Cost) return false;

        // Unity numbers the owned level from 1 (its CredP ramp indexes by it).
        _owned.Add(new OwnedHub(level.Name, career!.Id, level.Cost, tierId, levelIndex + 1));
        CredP -= level.Cost;
        return true;
    }

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
