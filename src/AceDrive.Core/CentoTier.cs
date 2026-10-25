// Cento Miglia event tiers (PORTPLAN item 49, port of Unity CentoMigliaScreen.
// BuildEvents): one club row per tier. Pure data: labels, descriptions and the
// reputation level they ask for, loaded from Data/cento_miglia.json. The era
// tier's label carries a {0} placeholder for the game year's decade, which the
// page formats in, so no sentence is assembled here.
namespace AceDrive.Core;

/// <summary>
/// One Cento Miglia tier: the club that runs the race, the line printed inside
/// its button and the detail page's description. The entry fee is club
/// reputation, never cash (Unity's reputationRequired).
/// </summary>
public sealed record CentoTier(
    string Id,
    string Label,
    string Description,
    int ReputationRequired);
