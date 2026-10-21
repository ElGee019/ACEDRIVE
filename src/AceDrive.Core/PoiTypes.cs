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
}
