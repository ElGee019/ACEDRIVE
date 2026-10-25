// Cento Miglia rules (PORTPLAN item 49, port of Unity CentoMigliaScreen.
// IsTierEligible + the header's reputation floor). Pure logic, no UI strings:
// the page formats these answers, so a future Lua port can transliterate the
// gates without carrying copy.
namespace AceDrive.Core;

/// <summary>
/// The event's two rules that outlive the page: who may enter a tier, and the
/// reputation the header prints. Unity keeps both inside the screen; the port
/// keeps them here so the page only formats what it is told and the tests can
/// pin a rule instead of a screenshot.
/// </summary>
public static class CentoMiglia
{
    /// <summary>The one tier whose entry ignores club reputation (Unity's
    /// <c>tier.id != "gentlemen_drivers"</c> exemption).</summary>
    public const string GentlemenDrivers = "gentlemen_drivers";

    /// <summary>
    /// Game year -&gt; its decade (Unity CentoMigliaScreen.BuildEvents:
    /// <c>(gameYear / 10) * 10</c>), which labels the era-only tier.
    /// </summary>
    public static int EraDecade(int year) => year / 10 * 10;

    /// <summary>
    /// The reputation the page prints: a first-ever driver's raw 0 reads as 1
    /// (Unity's header floor — the race ticker treats 0 the same way).
    /// </summary>
    public static double DisplayReputation(double clubReputation) =>
        clubReputation > 0.0 ? clubReputation : 1.0;

    /// <summary>
    /// Unity IsTierEligible's two live gates: the free tier ignores reputation
    /// and every other tier needs its level, and no tier opens for a driver
    /// without a discovered street (the map is what the race drives on). Unity's
    /// third gate — one entry per week, keyed on the Mille Miglia history entry
    /// — has no port yet: the race that writes that entry does not exist, so no
    /// week can already be used up.
    /// </summary>
    public static bool IsTierEligible(
        CentoTier tier, double clubReputation, bool hasDiscoveredStreets) =>
        hasDiscoveredStreets
        && (tier.Id == GentlemenDrivers || clubReputation >= tier.ReputationRequired);
}
