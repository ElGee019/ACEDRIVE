// POI type tags (PORTPLAN item 24, Unity IPoiTypeHandler): the free-form
// type string content and the arrival handlers match on. Constants keep the
// data file and the handler from spelling one tag two ways.
namespace AceDrive.Core;

/// <summary>POI type tags shared by content and the arrival handlers.</summary>
public static class PoiTypes
{
    /// <summary>Grocery shop (Unity's "grocery_store" family).</summary>
    public const string Grocery = "grocery";

    /// <summary>Generic grocery-shop tag Unity's generated stores use.</summary>
    public const string GroceryStore = "grocery_store";

    /// <summary>Unity's generated coffee-shop tag (the coffee hub's stores).</summary>
    public const string CoffeeShop = "coffee_shop";

    /// <summary>Max's story-shop tag Unity's special row uses.</summary>
    public const string MaxsGrocery = "maxs_grocery_store";

    /// <summary>
    /// A named resident's house (Unity's POIData type "person"): no counter and
    /// no shelf, so no shop handler claims it — the pickup slice will.
    /// </summary>
    public const string Person = "person";

    /// <summary>
    /// Grandma's house (Unity's "home" POI): the tour's final stop, where the
    /// PANCAKES arrival line opens the pancake dialogue.
    /// </summary>
    public const string Home = "home";

    /// <summary>
    /// The town's community centre (Unity's "communitycenter" POI): the
    /// post-pancake stop where the taxi license dialogue opens.
    /// </summary>
    public const string CommunityCentre = "communitycenter";

    /// <summary>The savings bank's interior (PORTPLAN 48).</summary>
    public const string Bank = "bank";

    /// <summary>The car dealer's weekly floor (PORTPLAN 29).</summary>
    public const string Dealer = "dealer";

    /// <summary>The car wash (PORTPLAN 33).</summary>
    public const string CarWash = "carwash";

    /// <summary>The car mechanic (PORTPLAN items 31/32).</summary>
    public const string Mechanic = "mechanic";

    /// <summary>The insurance broker (PORTPLAN 34).</summary>
    public const string Insurance = "insurance";

    /// <summary>A wholesale hub's pickup door (Unity's "hub" family): the
    /// delivery run loads the cart's freight here (PORTPLAN 19).</summary>
    public const string Hub = "hub";

    /// <summary>
    /// The weekly market-valuation venue (Unity ValuationHandler.MarkerKey):
    /// a temporary door, not a business. No typed handler owns it; the
    /// valuation flow watches the arrival itself.
    /// </summary>
    public const string Valuation = "valuation";

    /// <summary>
    /// The missing-person run's temporary marker (ToWhereIsWilly.md §3.1): the
    /// home door, the wrong-address door and a search stop. Never
    /// <see cref="Person"/>: that tag is the friend pickup's story arrival.
    /// </summary>
    public const string Willy = "willy";
}
