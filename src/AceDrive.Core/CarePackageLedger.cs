// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Care packages (PORTPLAN item 21, port of Unity CarePackageService +
// PlayerData.carePackages/carePackagesCarried): a completed ToDo drops one kit
// at a store in town, the player picks kits up there and drives them to the
// community centre. This class owns the counts and the placements; the run,
// the store draw and the save are the caller's.
namespace AceDrive.Core;

/// <summary>
/// The outstanding care kits and where they wait. A kit is either on a store's
/// shelf (<see cref="Outstanding"/>) or in the car (<see cref="Carried"/>); the
/// total is the pair, so a pickup never loses a kit and a refresh can neither
/// duplicate nor drop one. Seed the draw from a saved <see cref="CarePackageState"/>
/// to resume a run that quit mid-drive.
/// </summary>
public sealed class CarePackageLedger
{
    /// <summary>Kits the career may hold at once (Unity TodoManager.MaxCarePackages).</summary>
    public const int MaxPackages = 7;

    /// <summary>Kits one delivery run carries (Unity CarePackageDeliveryHandler.MaxBagsPerRun).</summary>
    public const int MaxBagsPerRun = 5;

    /// <summary>Kits that unlock DELIVER CARE PACKAGES (Unity TipsCompletedScreen.CarePackageBagThreshold).</summary>
    public const int BagThreshold = 5;

    // One entry per store, in grant order. A repeated draw at the same store
    // bumps its count instead of adding a second row (Unity CarePackagePoiEntry).
    private readonly List<CarePackagePlacement> _stores = [];

    public CarePackageLedger(CarePackageState? saved = null)
    {
        Restore(saved);
    }

    /// <summary>Kits still waiting on a store shelf.</summary>
    public int Outstanding
    {
        get
        {
            int total = 0;
            foreach (CarePackagePlacement placement in _stores)
            {
                total += placement.Count;
            }

            return total;
        }
    }

    /// <summary>Kits picked up but not yet dropped off at the community centre.</summary>
    public int Carried { get; private set; }

    /// <summary>Every kit the career still owes the neighbourhood.</summary>
    public int Total => Outstanding + Carried;

    /// <summary>True when the grant cap leaves no room for another kit.</summary>
    public bool IsFull => Total >= MaxPackages;

    /// <summary>True once a pickup put kits in the car: the run resumes at the drop-off.</summary>
    public bool HasBagsInCar => Carried > 0;

    /// <summary>The store shelves as the driver file stores them, in grant order.</summary>
    public IReadOnlyList<CarePackagePlacement> Stores => _stores;

    /// <summary>
    /// Drops one kit at <paramref name="storeId"/> (Unity
    /// CarePackageService.GrantCarePackage). False at the cap or for an unusable
    /// door id — the caller reports the lost grant instead of inventing a kit
    /// that no store can hand over.
    /// </summary>
    public bool Grant(string storeId)
    {
        if (IsFull || string.IsNullOrWhiteSpace(storeId))
        {
            return false;
        }

        AddToStore(storeId, 1);
        return true;
    }

    /// <summary>
    /// The pickup legs one run may serve: stores in grant order, capped at
    /// <see cref="MaxBagsPerRun"/> kits in total. The ledger is not touched —
    /// a kit leaves its shelf only when <see cref="Pickup"/> actually picks it
    /// up, so an abandoned run keeps every kit where it was.
    /// </summary>
    public IReadOnlyList<CarePackagePlacement> AllocateRun()
    {
        List<CarePackagePlacement> legs = [];
        int remaining = MaxBagsPerRun;
        foreach (CarePackagePlacement placement in _stores)
        {
            if (remaining <= 0)
            {
                break;
            }

            int take = Math.Min(placement.Count, remaining);
            if (take <= 0)
            {
                continue;
            }

            legs.Add(new CarePackagePlacement(placement.StoreId, take));
            remaining -= take;
        }

        return legs;
    }

    /// <summary>
    /// Moves up to <paramref name="count"/> kits from one store shelf into the
    /// car, returning how many actually moved. The total never changes here,
    /// so a restart between pickup and drop-off still owes every kit.
    /// </summary>
    public int Pickup(string storeId, int count)
    {
        if (count <= 0 || string.IsNullOrWhiteSpace(storeId))
        {
            return 0;
        }

        for (int i = 0; i < _stores.Count; i++)
        {
            if (!string.Equals(_stores[i].StoreId, storeId, StringComparison.Ordinal))
            {
                continue;
            }

            int picked = Math.Min(count, _stores[i].Count);
            if (picked <= 0)
            {
                return 0;
            }

            int left = _stores[i].Count - picked;
            if (left <= 0)
            {
                _stores.RemoveAt(i);
            }
            else
            {
                _stores[i] = _stores[i] with { Count = left };
            }

            Carried += picked;
            return picked;
        }

        return 0;
    }

    /// <summary>
    /// Hands every carried kit to the community centre, returning how many were
    /// delivered. That is the only moment the career's total drops.
    /// </summary>
    public int Deliver()
    {
        int delivered = Carried;
        Carried = 0;
        return delivered;
    }

    /// <summary>The state the driver file reopens, copied so a later grant cannot move it.</summary>
    public CarePackageState Snapshot() => new([.. _stores], Carried);

    // A saved row that cannot name a store or holds no kits is dropped: it can
    // never be picked up, and keeping it would hide a corrupt file behind a
    // count the player can see but not spend. Duplicate ids merge, which is the
    // same repair a hand-edited save needs.
    private void Restore(CarePackageState? saved)
    {
        Carried = Math.Max(0, saved?.Carried ?? 0);
        if (saved?.Stores is not { } stores)
        {
            return;
        }

        foreach (CarePackagePlacement? placement in stores)
        {
            if (placement is null
                || string.IsNullOrWhiteSpace(placement.StoreId)
                || placement.Count <= 0)
            {
                continue;
            }

            int room = MaxPackages - Total;
            if (room <= 0)
            {
                break;
            }

            AddToStore(placement.StoreId, Math.Min(placement.Count, room));
        }
    }

    private void AddToStore(string storeId, int count)
    {
        for (int i = 0; i < _stores.Count; i++)
        {
            if (string.Equals(_stores[i].StoreId, storeId, StringComparison.Ordinal))
            {
                _stores[i] = _stores[i] with { Count = _stores[i].Count + count };
                return;
            }
        }

        _stores.Add(new CarePackagePlacement(storeId, count));
    }
}
