// Pure gameplay data for the Saturday Touristenfahrten delivery run: the
// pickup shop rows (company names + order composition), the dropoff places and
// the run's draw bounds. Data/touristenfahrten.json is the source of truth;
// Default is the test-shell fallback and the deterministic fixture.
namespace AceDrive.Core;

/// <summary>
/// The Touristenfahrten content table: which shops can load an order, which
/// corners it may be delivered to, and how many of each a run draws. Balance
/// numbers stay here (data file first, this fallback second), never in the
/// flow or the page.
/// </summary>
/// <param name="Pickups">Pickup shop kinds in definition order.</param>
/// <param name="Dropoffs">Places a run may deliver to, drawn without repeats.</param>
/// <param name="MinDropoffs">Fewest dropoffs one run carries.</param>
/// <param name="MaxDropoffs">Most dropoffs one run carries.</param>
/// <param name="MinItemsPerOrder">Fewest items one dropoff's order holds; the
/// draw tops every order up to it.</param>
public sealed record TouristenMenu(
    IReadOnlyList<TouristenPickup> Pickups,
    IReadOnlyList<TouristenDropoffDefinition> Dropoffs,
    int MinDropoffs,
    int MaxDropoffs,
    int MinItemsPerOrder)
{
    /// <summary>
    /// The built-in table, used when the data file is missing or unusable and
    /// pinned by the tests. Prices follow the shipped content; the tiramisu
    /// price is the store catalogue's own (7.00).
    /// </summary>
    public static TouristenMenu Default { get; } = new(
        [
            new TouristenPickup(
                "coffee",
                PoiTypes.CoffeeShop,
                "coffee_beans",
                ["Café am Ring", "Kaffeehaus Sonne", "Rösterei Eifel"],
                [
                    new TouristenItemRule("coffee", "TouristenItemCoffee", 3.5, 0, 10),
                    new TouristenItemRule("beer", "TouristenItemBeer", 5.5, 0, 10),
                    new TouristenItemRule(
                        "currywurst_pommes", "TouristenItemCurrywurstPommes", 9.5, 0, 10),
                    new TouristenItemRule("schnitzel", "TouristenItemSchnitzel", 19.5, 0, 10),
                    new TouristenItemRule(
                        "bratwurst_kraut", "TouristenItemBratwurstKraut", 7.0, 0, 10),
                ]),
            new TouristenPickup(
                "pizza",
                PoiTypes.PizzaPlace,
                "pizza",
                ["Pizzeria da Luigi", "Pizzeria Napoli", "Pizza am Ring"],
                [
                    new TouristenItemRule("pizza_slice", "TouristenItemPizzaSlice", 5.0, 1, 10),
                    new TouristenItemRule("beer", "TouristenItemBeer", 5.5, 0, 10),
                    new TouristenItemRule("espresso", "TouristenItemEspresso", 3.0, 0, 10),
                    new TouristenItemRule("tiramisu", "TouristenItemTiramisu", 7.0, 0, 10),
                    new TouristenItemRule("soup", "TouristenItemSoup", 8.5, 0, 10),
                ]),
        ],
        [
            new TouristenDropoffDefinition("touristen-brunchen", "Brünnchen"),
            new TouristenDropoffDefinition("touristen-einfahrt", "Einfahrt"),
            new TouristenDropoffDefinition("touristen-pflanzgarten", "Pflanzgarten"),
            new TouristenDropoffDefinition("touristen-adenauer-forst", "Adenauer Forst"),
            new TouristenDropoffDefinition("touristen-breidscheid", "Breidscheid Bridge"),
        ],
        MinDropoffs: 1,
        MaxDropoffs: 3,
        MinItemsPerOrder: 2);

    /// <summary>
    /// True when the table can drive a run: both a pickup and a dropoff exist,
    /// the draw bounds make sense and every item range is honest. A hand-edited
    /// file that fails this falls back to <see cref="Default"/> instead of
    /// producing an unplayable run.
    /// </summary>
    public bool IsUsable
    {
        get
        {
            if (Pickups is not { Count: > 0 } || Dropoffs is not { Count: > 0 })
            {
                return false;
            }

            if (MinDropoffs < 1 || MaxDropoffs < MinDropoffs || MinItemsPerOrder < 2)
            {
                return false;
            }

            foreach (TouristenPickup pickup in Pickups)
            {
                if (string.IsNullOrWhiteSpace(pickup.Kind)
                    || string.IsNullOrWhiteSpace(pickup.PoiType)
                    || pickup.Items is not { Count: > 0 })
                {
                    return false;
                }

                foreach (TouristenItemRule item in pickup.Items)
                {
                    if (string.IsNullOrWhiteSpace(item.Key)
                        || string.IsNullOrWhiteSpace(item.NameKey)
                        || item.Min < 0
                        || item.Max < item.Min
                        || !double.IsFinite(item.Price)
                        || item.Price <= 0.0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    /// <summary>
    /// The pickup row that owns a POI family, or null when the door is not one
    /// of this run's shops. The directory's known coffee shops and pizzerias
    /// resolve through here, so the draw never needs a hardcoded type list.
    /// </summary>
    public TouristenPickup? PickupFor(string? poiType)
    {
        if (string.IsNullOrWhiteSpace(poiType))
        {
            return null;
        }

        foreach (TouristenPickup pickup in Pickups)
        {
            if (string.Equals(pickup.PoiType, poiType, StringComparison.OrdinalIgnoreCase))
            {
                return pickup;
            }
        }

        return null;
    }
}
