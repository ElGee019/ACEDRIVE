// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: ToWhereIsWilly.md §2.7 / §3.1 (Unity WillyReceiptScreen and
// ScreenArgs.WillyReceiptArgs): the data the receipt page paints, nothing more.
namespace AceDrive.Core;

/// <summary>
/// One settled missing-person run, ready to render: who was returned, where
/// they were found, the finder's fee with every multiplier that priced it, the
/// calendar week the certificate names and the friendship the run left behind.
/// The flow pays and stamps before the page opens, so the page only formats
/// these numbers.
/// </summary>
public sealed record WhereIsWillyReceipt(
    string PersonName,
    string FirstName,
    string ShopName,
    int FinderFee,
    double AverageCarValue,
    double BaseReward,
    int ShopCount,
    double ShopMultiplier,
    int Attempts,
    double AttemptMultiplier,
    int Week,
    int Year,
    int FriendshipLevel)
{
    /// <summary>True when the hiding shop was the first one visited: the
    /// receipt prints the "1st try" line instead of an attempt number.</summary>
    public bool FirstTry => Attempts == 0;
}
