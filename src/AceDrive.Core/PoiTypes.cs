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
}
