// The live shops of the town (PORTPLAN items 24/26, port of Unity
// StorePlacementHelper.GenerateStoreItems + StoreState creation): a store's
// item set is drawn once from the catalog layout, with the story item
// guaranteed, and kept for the session. The seed is the POI's address, so
// re-entering the same shop shows the same shelves without a save file.
namespace AceDrive.Core;

/// <summary>One live <see cref="StoreInventory"/> per POI, created on first entry.</summary>
public sealed class StoreNetwork
{
    private readonly StoreCatalog _catalog;
    private readonly Dictionary<string, PoiDefinition> _definitions;
    private readonly Dictionary<string, StoreInventory> _live =
        new(StringComparer.OrdinalIgnoreCase);

    public StoreNetwork(StoreCatalog catalog, IReadOnlyList<PoiDefinition>? definitions = null)
    {
        _catalog = catalog ?? StoreCatalog.Default;
        var byId = new Dictionary<string, PoiDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (PoiDefinition definition in definitions ?? [])
        {
            // A duplicate id is a data bug; first row wins so a shop lookup
            // can never throw mid-entry.
            byId.TryAdd(definition.Id, definition);
        }
        _definitions = byId;
    }

    /// <summary>
    /// The shop's live inventory, drawn on first entry and cached after.
    /// Never throws: a missing layout or category degrades to every item in
    /// the catalogue so the page is never an empty dead end.
    /// </summary>
    public StoreInventory For(PointOfInterest poi)
    {
        if (_live.TryGetValue(poi.Id, out StoreInventory? existing))
        {
            return existing;
        }

        var inventory = new StoreInventory(
            poi.Id, ChooseItems(poi), _catalog.DefaultStock);
        _live[poi.Id] = inventory;
        return inventory;
    }

    // Unity GenerateStoreItems draws from each source category, then Max's
    // screen guarantees its quest item. Ordering the layout keys keeps the
    // draw deterministic across runs.
    private List<StoreItem> ChooseItems(PointOfInterest poi)
    {
        var random = new Random(StableSeed(poi));
        var selected = new List<StoreItem>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // JSON can carry a null category on a hand-edited row; an empty key
        // simply misses the layout and falls through to the all-items tail.
        string category = poi.Category ?? string.Empty;
        IReadOnlyDictionary<string, int>? layout = null;
        _catalog.Layouts?.TryGetValue(category, out layout);
        if (layout is { Count: > 0 })
        {
            foreach ((string source, int count) in
                layout.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (count <= 0)
                {
                    continue;
                }

                var pool = new List<StoreItem>(_catalog.ByCategory(source));
                Shuffle(pool, random);
                int taken = 0;
                foreach (StoreItem item in pool)
                {
                    if (!used.Add(item.Name))
                    {
                        continue;
                    }
                    selected.Add(item);
                    taken++;
                    if (taken >= count)
                    {
                        break;
                    }
                }
            }
        }
        else
        {
            foreach (StoreItem item in _catalog.ByCategory(category))
            {
                if (used.Add(item.Name))
                {
                    selected.Add(item);
                }
            }
        }

        foreach (string name in GuaranteedFor(poi))
        {
            if (used.Contains(name))
            {
                continue;
            }
            StoreItem? item = _catalog.Find(name);
            if (item is not null)
            {
                selected.Add(item);
                used.Add(item.Name);
            }
        }

        // A layout/category mismatch must still leave a shoppable page.
        if (selected.Count == 0)
        {
            foreach (StoreItem item in _catalog.Items)
            {
                if (used.Add(item.Name))
                {
                    selected.Add(item);
                }
            }
        }

        return selected;
    }

    private IReadOnlyList<string> GuaranteedFor(PointOfInterest poi) =>
        _definitions.TryGetValue(poi.Id, out PoiDefinition? definition)
            && definition.GuaranteedItems is { } items
                ? items
                : [];

    // FNV-1a over the POI identity and address: string.GetHashCode is
    // process-randomized, so it cannot make the shelves stable.
    private static int StableSeed(PointOfInterest poi)
    {
        uint hash = 2166136261;
        hash = Add(hash, poi.Id);
        hash = Add(hash, poi.Address.StreetName);
        hash = Add(hash, poi.Address.HouseNumber.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        return unchecked((int)hash);
    }

    private static uint Add(uint hash, string value)
    {
        foreach (char c in value)
        {
            hash ^= c;
            hash *= 16777619;
        }
        hash ^= 0xFF;
        return hash * 16777619;
    }

    private static void Shuffle(List<StoreItem> items, Random random)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
