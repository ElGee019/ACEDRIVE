// Road-test receipt balance (PORTPLAN item 49, port of Unity
// RoadTestAudienceCalculator + RoadTestReceiptScreen.PopulateReceipt): the flat
// forfeit, the magazine's price divisor and the commission the issue pays.
// Balance lives in Data/road_test.json; Default is the test-shell fallback, not
// the source of truth (AGENTS: no magic balance values).
namespace AceDrive.Core;

/// <summary>
/// The money behind one road-test issue. Unity keeps these as literals inside
/// RoadTestAudienceCalculator; the port keeps them in one record so the JSON
/// store owns the content and the tests pin values instead of a screenshot.
/// </summary>
/// <param name="BaseForfeit">CR the magazine pays whatever the crowd did
/// (Unity's BaseForfeit: 400).</param>
/// <param name="MagazinePriceDivisor">The car journalist reputation this
/// divides into the magazine price (Unity's <c>/ 2f</c>).</param>
/// <param name="CommissionPercent">The share of the crowd's copies the issue
/// keeps (Unity's <c>/ 10f</c>, i.e. 10%).</param>
public sealed record RoadTestAudienceRules(
    int BaseForfeit,
    double MagazinePriceDivisor,
    double CommissionPercent)
{
    /// <summary>Unity's literals: the shipped values live in Data/road_test.json,
    /// this copy is the deterministic fallback.</summary>
    public static readonly RoadTestAudienceRules Default = new(
        BaseForfeit: 400,
        MagazinePriceDivisor: 2.0,
        CommissionPercent: 10.0);
}
