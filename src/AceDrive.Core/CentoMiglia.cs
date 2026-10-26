// Cento Miglia rules (PORTPLAN item 49, port of Unity CentoMigliaScreen):
// the tier gates, the header's reputation floor, the era decade and the ENTER
// flow's registration and route rules. Pure logic, no UI strings: the page
// formats these answers, so a future Lua port can transliterate the rules
// without carrying copy.
namespace AceDrive.Core;

/// <summary>
/// The event's rules that outlive the page: who may enter a tier, the
/// reputation the header prints, and what entering builds — the tier's
/// registration number and the longest route the operating zone allows. Unity
/// keeps them inside the screen; the port keeps them here so the page only
/// formats what it is told and the tests can pin a rule instead of a
/// screenshot.
/// </summary>
public static class CentoMiglia
{
    /// <summary>The one tier whose entry ignores club reputation (Unity's
    /// <c>tier.id != "gentlemen_drivers"</c> exemption).</summary>
    public const string GentlemenDrivers = "gentlemen_drivers";

    /// <summary>
    /// Unity OnEnterEventClicked's minimum setup delay: the page shows the
    /// event being built for at least this long, whether the route resolves
    /// instantly or not, so the registration number is read before the race.
    /// </summary>
    public const int BuildSeconds = 4;

    /// <summary>
    /// Game year -&gt; its decade (Unity CentoMigliaScreen.BuildEvents:
    /// <c>(gameYear / 10) * 10</c>). Tier labels are data-driven, so the page
    /// substitutes it wherever the table asks for it with <c>{0}</c>.
    /// </summary>
    public static int EraDecade(int year) => year / 10 * 10;

    /// <summary>
    /// The registration number's digit count over the event tiers, Unity's
    /// <c>ResolveSelectedTierIndex</c> table: T1 one digit, T2-T3 two, T4-T6
    /// three, T7-T8 four. The number is the event's identity on the race
    /// sheet, so the field grows with the club that issues it.
    /// </summary>
    public static int RegistrationDigits(int tierNumber) => tierNumber switch
    {
        <= 1 => 1,
        <= 3 => 2,
        <= 6 => 3,
        _ => 4,
    };

    /// <summary>
    /// Draws the registration number a tier issues (Unity's
    /// <c>Random.Range(regLow, regHigh)</c>): a whole number of the tier's
    /// digit count, so the first club can issue 0 and every later one starts
    /// at the smallest number with that many digits.
    /// </summary>
    public static int DrawRegistration(int tierNumber, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        int digits = RegistrationDigits(tierNumber);
        int high = (int)Math.Pow(10, digits);
        // Unity's one-digit draw starts at 0, not at 1.
        int low = digits == 1 ? 0 : high / 10;
        return rng.Next(low, high);
    }

    /// <summary>
    /// The event's route length: the longest distance the player's operating
    /// zone allows, which is the zone itself — a route across every discovered
    /// street (Unity ranks its discovered street pairs and takes the furthest).
    /// An unusable zone length answers 0 instead of throwing on the page.
    /// </summary>
    public static double LongestDistanceKm(double zoneKm) =>
        double.IsFinite(zoneKm) && zoneKm > 0.0 ? zoneKm : 0.0;

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
