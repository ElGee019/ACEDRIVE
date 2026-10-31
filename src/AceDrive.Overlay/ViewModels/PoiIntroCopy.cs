using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The explanation copy of the POI families a zone expansion announces
/// (TOZONES §4.5): one resx pair per type, in the same data-first shape as
/// <see cref="PoiTypeLabels"/>. A family without authored copy (the store
/// doors and the pizzeria the port has no page for yet) answers null, so the
/// page is simply skipped and a new content row can never print a resx key.
/// </summary>
public static class PoiIntroCopy
{
    /// <summary>The page copy for one POI type, or null when none is authored.</summary>
    public static PoiIntro? For(string? type) => type switch
    {
        PoiTypes.CarWash => Read("PoiIntroCarWash"),
        PoiTypes.Insurance => Read("PoiIntroInsurance"),
        PoiTypes.Bank => Read("PoiIntroBank"),
        PoiTypes.Dealer => Read("PoiIntroDealer"),
        PoiTypes.Mechanic => Read("PoiIntroMechanic"),
        _ => null,
    };

    /// <summary>
    /// The pages the expansion page's CONTINUE opens, one per family in the
    /// order the growth banked the doors. The page is not a first-time event:
    /// a family the directory already knew gets it again on its next door.
    /// </summary>
    public static IReadOnlyList<PoiIntro> ForAdded(IReadOnlyList<PointOfInterest> added)
    {
        ArgumentNullException.ThrowIfNull(added);
        var intros = new List<PoiIntro>(added.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PointOfInterest poi in added)
        {
            if (seen.Add(poi.Type) && For(poi.Type) is { } intro)
            {
                intros.Add(intro);
            }
        }

        return intros;
    }

    // A record per page open is enough: opening the page is a cold settle
    // event, and the resx strings themselves are already cached by the
    // ResourceManager, so there is nothing here to keep between openings.
    private static PoiIntro Read(string key) =>
        new(Strings.Get(key + "Title"), Strings.Get(key + "Body"));
}
