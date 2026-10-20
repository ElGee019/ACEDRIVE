using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Shared receipt copy assembly (taxi + delivery + car-haul receipts): the
/// €-prefixed amount column, the per-tier explanation sentences, the
/// familiarity sentence and the drive-duration line. Copy lives in resx so
/// translators own it; this helper only binds data to format keys, one
/// place so the receipts can never drift apart in wording or currency.
/// </summary>
public static class ReceiptText
{
    /// <summary>The €-prefixed amount column; the format lives in resx so
    /// the currency placement stays translatable.</summary>
    public static string Amount(double value) => Strings.Format("ReceiptAmount", value);

    /// <summary>One explanation row per tier the charge walked (Unity
    /// hides unused rows); resx wording per band so translators reorder.</summary>
    public static List<ReceiptLine> TierLines(IReadOnlyList<ReceiptTier> tiers)
    {
        List<ReceiptLine> lines = [];
        foreach (ReceiptTier tier in tiers)
        {
            string explanation = tier.IsOpen
                ? Strings.Format("ReceiptTierOpen", tier.StartKm, tier.RatePerKm, tier.Km)
                : Strings.Format(
                    "ReceiptTier", tier.StartKm, tier.StartKm + tier.WidthKm,
                    tier.RatePerKm, tier.Km);
            lines.Add(new ReceiptLine(explanation, Amount(tier.Charge)));
        }
        return lines;
    }

    /// <summary>Unity familiarity sentence (FriendshipHelper bands), keyed
    /// by the tier so every band keeps its own copy.</summary>
    public static string FriendshipSentence(string ownerName, int level) =>
        Strings.Format(
            FriendshipKey(level), FriendshipLevels.DisplayFirstName(ownerName), level);

    /// <summary>Unity's drive-duration line (CarHaulingScreen's hours +
    /// minutes wording, whose English plural it inlines): the resx sentence
    /// carries word slots and this helper fills them, so the singular
    /// vs plural choice stays one comparison on the count, in one place.
    /// The words themselves are resx keys (nl: both hour words are "uur").
    /// </summary>
    public static string DriveDuration(int hours, int minutes) => Strings.Format(
        "HaulReceiptTime", hours,
        Strings.Get(hours == 1 ? "HaulReceiptHour" : "HaulReceiptHours"),
        minutes,
        Strings.Get(minutes == 1 ? "HaulReceiptMinute" : "HaulReceiptMinutes"));

    // resx key per band, translator-owned.
    private static string FriendshipKey(int level) => FriendshipLevels.GetTier(level) switch
    {
        0 => "FriendshipStranger",
        1 => "FriendshipAcquaintance",
        2 => "FriendshipFriend",
        3 => "FriendshipGood",
        4 => "FriendshipClose",
        _ => "FriendshipBff",
    };
}
