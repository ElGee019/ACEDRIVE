// Fixed story people (PORTPLAN items 5/27, Unity's justins_place POIData):
// identity only, because the house is drawn per session and stored with the
// onboarding progress. They are NOT in PoiCatalog — the errand generator draws
// shops, never people — so this is the built-in data table for residents.
namespace AceDrive.Core;

/// <summary>The friends the introduction sends the player to.</summary>
public static class StoryFriends
{
    /// <summary>
    /// Justin: the childhood friend who still lives in the street where the
    /// player grew up (Unity's post-Max task line).
    /// </summary>
    public static PoiDefinition Justin { get; } = new(
        "justins_place", "Justin's Place", PoiTypes.Person, "Justin");

    /// <summary>
    /// Annabella: the second pickup of the same street tour, drawn from the
    /// same houses as Justin (Unity's CreateFriendPOIsFromHouses).
    /// </summary>
    public static PoiDefinition Annabella { get; } = new(
        "annabellas_place", "Annabella's Place", PoiTypes.Person, "Annabella");
}
