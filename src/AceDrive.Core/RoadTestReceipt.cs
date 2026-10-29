// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// What one finished road test pays and publishes (PORTPLAN item 49, Unity
// RoadTestScreen.PopulateReceipt + RoadTestReceiptScreen): the shoot's door,
// the crowd it reached, the variety against the last location, the car
// journalist's reputation, the magazine price and the earnings, each beside
// the previous issue's reading and the personal best. Core returns DATA; the
// receipt page formats every sentence from the resource files.
namespace AceDrive.Core;

/// <summary>
/// One settled road test (PORTPLAN item 49): the numbers Unity's
/// RoadTestReceiptScreen prints. The <c>Previous*</c> members are the issue
/// before this one, so the page can show "(previously …)" exactly like Unity;
/// the <c>Highest*</c> members are the bests after this issue, i.e. already
/// including it. <paramref name="PhotoOffsetMeters"/> is the lap offset the
/// shoot was taken at, which the next issue measures its variety against.
/// </summary>
public sealed record RoadTestReceipt(
    string CarName,
    string PhotoStreet,
    int PhotoHouseNumber,
    double PhotoOffsetMeters,
    int Citizens,
    int Friends,
    int Acquaintances,
    int PeopleReached,
    int PreviousPeopleReached,
    double Variety,
    double PreviousVariety,
    int ResultReputation,
    int NaturalReputation,
    double JournalistReputation,
    double PreviousJournalistReputation,
    double MagazinePrice,
    double PreviousMagazinePrice,
    int Forfeit,
    double CommissionPercent,
    double Commission,
    double Earnings,
    double PreviousEarnings,
    int HighestPeopleReached,
    double HighestJournalistReputation,
    double HighestMagazinePrice,
    double HighestEarnings,
    bool FirstPhoto,
    double DistanceKm,
    double MaxDistanceKm)
{
    /// <summary>The CR the issue banked (Unity's total earnings).</summary>
    public double Payment => Earnings;

    /// <summary>True when the shoot was taken at a resolvable door.</summary>
    public bool HasPhotoLocation => PhotoStreet.Length > 0 && PhotoHouseNumber > 0;

    /// <summary>
    /// The record this issue leaves behind for the next one: its readings become
    /// the "previous" baseline, its highs become the bests and its door becomes
    /// the variety baseline.
    /// </summary>
    public RoadTestRecord ToRecord() => new(
        NaturalReputation,
        HasPhoto: HasPhotoLocation,
        PhotoStreet: HasPhotoLocation ? PhotoStreet : null,
        PhotoHouseNumber: HasPhotoLocation ? PhotoHouseNumber : 0,
        PhotoOffsetMeters: HasPhotoLocation ? PhotoOffsetMeters : 0.0,
        LastPeopleReached: PeopleReached,
        LastVariety: Variety,
        LastJournalistReputation: JournalistReputation,
        LastMagazinePrice: MagazinePrice,
        LastEarnings: Earnings,
        HighestPeopleReached: HighestPeopleReached,
        HighestJournalistReputation: HighestJournalistReputation,
        HighestMagazinePrice: HighestMagazinePrice,
        HighestEarnings: HighestEarnings);
}
