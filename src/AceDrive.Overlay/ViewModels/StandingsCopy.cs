using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The gained/lost sentence every standings block shows (Unity's receipt and
/// shift-ended screens build it from the same literals). Kept in one place so
/// a second board cannot drift from the first; the rank line itself stays per
/// board (XP vs. taxi shift copy).
/// </summary>
internal static class StandingsCopy
{
    public static string Delta(int rankChange) => rankChange switch
    {
        1 => Strings.Get("ReceiptStandingsGainedOne"),
        > 0 => Strings.Format("ReceiptStandingsGained", rankChange),
        -1 => Strings.Get("ReceiptStandingsLostOne"),
        < 0 => Strings.Format("ReceiptStandingsLost", -rankChange),
        _ => Strings.Get("ReceiptStandingsNoChange"),
    };
}
