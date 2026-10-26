// Cento Miglia race balance (PORTPLAN item 49, port of Unity
// CentoMigliaScreen.ProcessReceiptRewards/PopulateReceipt): the placement
// payout ladder, the Star of the Show logistic curve and the generated item
// names. Balance lives in Data/cento_race.json; Default is the test-shell
// fallback, not the source of truth (AGENTS: no magic balance values).
namespace AceDrive.Core;

/// <summary>
/// The numbers behind one settled race. Unity keeps them as literals inside
/// CentoMigliaScreen; the port keeps them in one record so the JSON store owns
/// the content and the tests pin values instead of a screenshot.
/// </summary>
/// <param name="PlacementEarnings">CR per finishing place, first place first
/// (Unity's baseByPlace array). A place past the list pays nothing.</param>
/// <param name="StarBaseChance">The logistic's ceiling before placement scaling
/// (Unity's 0.90).</param>
/// <param name="StarSteepness">How fast the chance rises with the car's class
/// (Unity's 0.4 inside the exponent).</param>
/// <param name="StarClassPivot">The car class where the chance is half its
/// ceiling (Unity's 12).</param>
/// <param name="StarPlacementMultipliers">The place scaling of the chance,
/// first place first; the last entry repeats for any later place (Unity's
/// switch: 1.2, 1.0, 0.8, else 0.6).</param>
/// <param name="StarPrizePerWin">CR per Star of the Show win, multiplied by the
/// new win total (Unity's <c>newWins * 500</c>).</param>
/// <param name="AwardItemName">Item name template for a won award: {0} year,
/// {1} car.</param>
/// <param name="CertificateItemName">Item name template for a finishing
/// certificate: {0} week, {1} year, {2} tier.</param>
public sealed record CentoRaceRules(
    IReadOnlyList<double> PlacementEarnings,
    double StarBaseChance,
    double StarSteepness,
    int StarClassPivot,
    IReadOnlyList<double> StarPlacementMultipliers,
    double StarPrizePerWin,
    string AwardItemName,
    string CertificateItemName)
{
    /// <summary>Unity's literals, byte for byte: the shipped values live in
    /// Data/cento_race.json, this copy is the deterministic fallback.</summary>
    public static readonly CentoRaceRules Default = new(
        PlacementEarnings: [200.0, 150.0, 100.0, 50.0],
        StarBaseChance: 0.90,
        StarSteepness: 0.4,
        StarClassPivot: 12,
        StarPlacementMultipliers: [1.2, 1.0, 0.8, 0.6],
        StarPrizePerWin: 500.0,
        AwardItemName: "Star of the Show Award {0} - {1}",
        CertificateItemName: "Cento Miglia Certificate Week {0} {1} - Tier {2}");
}
