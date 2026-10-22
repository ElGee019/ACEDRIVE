namespace AceDrive.Overlay.Services;

/// <summary>
/// The EVO-namespaced Firestore collections. They share the LATESTDRIVEBS
/// project with the Unity game but never its boards: the two rosters use
/// different car ids, so mixing them would distort the rarity ranks.
/// </summary>
public static class FirestoreCollections
{
    public const string Players = "acevo_players";
    public const string Cars = "acevo_cars";
    public const string Countries = "acevo_countries";
    public const string PlayerNames = "acevo_playerNames";
    public const string Valuation = "acevo_enteredForValuation";
    public const string RoadTest = "acevo_roadTestStandings";
    public const string RoadTestBrake = "acevo_roadTestBrakeStandings";
}
