using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The resx label of a POI type tag (TOZONES §4.5): the expansion page lists a
/// business by its family, never by the raw machine tag. An unknown tag
/// degrades to Unity's GetFriendlyTypeLabel title-cased fallback, so a new
/// content row can never print "coffee_shop" on screen.
/// </summary>
public static class PoiTypeLabels
{
    /// <summary>The display label for one <see cref="PoiTypes"/> tag.</summary>
    public static string Of(string? type) => type switch
    {
        PoiTypes.CarWash => Strings.Get("PoiTypeCarWash"),
        PoiTypes.Bank => Strings.Get("PoiTypeBank"),
        PoiTypes.Insurance => Strings.Get("PoiTypeInsurance"),
        PoiTypes.Dealer => Strings.Get("PoiTypeDealer"),
        PoiTypes.Mechanic => Strings.Get("PoiTypeMechanic"),
        PoiTypes.GroceryStore => Strings.Get("PoiTypeGrocery"),
        PoiTypes.CoffeeShop => Strings.Get("PoiTypeCoffee"),
        _ => Friendly(type),
    };

    // Unity's fallback splits the underscore family tag into words and title-
    // cases them ("maxs_grocery_store" -> "Maxs Grocery Store").
    private static string Friendly(string? type) =>
        string.IsNullOrWhiteSpace(type)
            ? string.Empty
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(type.Replace('_', ' '));
}
