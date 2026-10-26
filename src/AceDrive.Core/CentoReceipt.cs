// Cento Miglia receipt (PORTPLAN item 49, port of Unity
// CentoMigliaScreen.ProcessReceiptRewards + PopulateReceipt): what one finished
// race pays. Pure data; the receipt page formats it and the settle banks it.
namespace AceDrive.Core;

/// <summary>
/// One settled Cento Miglia: the finish, the purse, the reputation the race
/// grew and the Star of the Show outcome. Unity keeps only the live
/// ActiveMilleMiglia and writes the fields straight onto PlayerData; the port
/// hands the page one record so the money, the reputation and the item are
/// priced together and can be asserted without a screen.
/// </summary>
/// <param name="Year">The game year the event was entered in.</param>
/// <param name="Week">The calendar week the event was entered in.</param>
/// <param name="Tier">The club tier that issued the event (1 to 8).</param>
/// <param name="ClubLabel">The tier's printed label.</param>
/// <param name="Placement">The finishing place (Unity's playerPlacement); the
/// port has no NPC field yet, so a solo race finishes first.</param>
/// <param name="PlacementEarnings">CR for the place: the ladder value times the
/// tier and the race-end reputation (Unity's placement earnings).</param>
/// <param name="StartReputation">The club reputation when the race started.</param>
/// <param name="EndReputation">The club reputation when it finished.</param>
/// <param name="EarnedReputation">What the race added, never negative.</param>
/// <param name="CarName">The car that ran the race, its display name.</param>
/// <param name="CarClass">That car's class, the Star of the Show input.</param>
/// <param name="OwnedCars">The garage size at the finish, the receipt's own line.</param>
/// <param name="StarWon">True when the Star of the Show roll succeeded.</param>
/// <param name="StarPrize">CR the award paid (zero on a certificate).</param>
/// <param name="StarWins">The career's Star of the Show wins after this race.</param>
/// <param name="ItemName">The generated award/certificate the driver receives.</param>
/// <param name="ConcentrationAward">The driver-rating points the place earns
/// (Unity: first place +2, second +1, the rest nothing).</param>
/// <param name="TotalEarned">The purse actually banked: placement plus prize.</param>
public sealed record CentoReceipt(
    int Year,
    int Week,
    int Tier,
    string ClubLabel,
    int Placement,
    double PlacementEarnings,
    double StartReputation,
    double EndReputation,
    double EarnedReputation,
    string CarName,
    int CarClass,
    int OwnedCars,
    bool StarWon,
    double StarPrize,
    int StarWins,
    string ItemName,
    double ConcentrationAward,
    double TotalEarned);
