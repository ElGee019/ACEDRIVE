// Cento Miglia receipt builder (PORTPLAN item 49, port of Unity
// CentoMigliaScreen.ProcessReceiptRewards + PopulateReceipt): the placement
// purse, the Star of the Show roll and the generated award/certificate. The
// Star of the Show roll rides a seeded Random so a test pins the outcome
// (AGENTS: randomness is a seeded instance, never the shared global).
namespace AceDrive.Core;

/// <summary>
/// Prices one finished Cento Miglia. Unity also awards placement-based driver
/// rating points and writes the race's reputation back to PlayerData; the
/// receipt carries both as data so the settle can bank them in its own order.
/// </summary>
public static class CentoReceiptBuilder
{
    /// <summary>
    /// Builds the receipt for a finished race. <paramref name="placement"/> is
    /// the finishing place (1 when the player is first), the reputation pair is
    /// the race's start/end reading, and <paramref name="previousStarWins"/>
    /// is the career total the prize scales on.
    /// </summary>
    public static CentoReceipt Build(
        CentoMigliaEvent race,
        int placement,
        double startReputation,
        double endReputation,
        string carName,
        int carClass,
        int ownedCars,
        int previousStarWins,
        CentoRaceRules rules,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(race);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(rng);

        int place = Math.Max(1, placement);
        double start = FiniteOr(startReputation, 0.0);
        double end = FiniteOr(endReputation, start);
        double earned = Math.Max(0.0, end - start);
        // Unity prices placement on the CURRENT reputation (the race's own
        // growth included) and truncates to whole CR; the port keeps its
        // 2-decimal ledger instead.
        double placePayout = PlacementPayout(place, race.TierNumber, end, rules);

        double chance = StarChance(place, carClass, rules);
        bool starWon = rng.NextDouble() < chance;
        int wins = starWon ? Math.Max(0, previousStarWins) + 1 : Math.Max(0, previousStarWins);
        double prize = starWon ? Cents(wins * Math.Max(0.0, rules.StarPrizePerWin)) : 0.0;

        string item = FormatItem(starWon, rules, race, carName);
        double total = Cents(placePayout + prize);

        return new CentoReceipt(
            race.Year,
            race.Week,
            race.TierNumber,
            race.TierLabel,
            place,
            placePayout,
            start,
            end,
            earned,
            carName ?? string.Empty,
            carClass,
            ownedCars,
            starWon,
            prize,
            wins,
            item,
            ConcentrationAward(place),
            total);
    }

    /// <summary>
    /// The chance of a Star of the Show, Unity's logistic: the class curve
    /// scaled by the finishing place. Exposed so a test can pin the curve and a
    /// log can explain a roll.
    /// </summary>
    public static double StarChance(int placement, int carClass, CentoRaceRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        double ceiling = Math.Clamp(rules.StarBaseChance, 0.0, 1.0);
        double logistic = ceiling
            / (1.0 + Math.Exp(-rules.StarSteepness * (carClass - rules.StarClassPivot)));
        double multiplier = rules.StarPlacementMultipliers.Count == 0
            ? 1.0
            : rules.StarPlacementMultipliers[
                Math.Clamp(placement - 1, 0, rules.StarPlacementMultipliers.Count - 1)];
        double chance = logistic * multiplier;
        return double.IsFinite(chance) ? Math.Clamp(chance, 0.0, 1.0) : 0.0;
    }

    // Unity's baseByPlace × max(1, tier) × current club reputation. A place
    // past the ladder earns nothing (Unity answers 0 the same way).
    private static double PlacementPayout(
        int placement, int tier, double reputation, CentoRaceRules rules)
    {
        int index = placement - 1;
        if (index < 0 || index >= rules.PlacementEarnings.Count)
        {
            return 0.0;
        }

        double reward = rules.PlacementEarnings[index] * Math.Max(1, tier)
            * Math.Max(0.0, reputation);
        return double.IsFinite(reward) ? Cents(reward) : 0.0;
    }

    // Unity DriverRatingService.ApplyConcentrationDelta at the receipt: a win
    // lifts concentration by 2, a second place by 1, later places not at all.
    private static double ConcentrationAward(int placement) => placement switch
    {
        1 => 2.0,
        2 => 1.0,
        _ => 0.0,
    };

    // The award names a win, the certificate a finish: Unity generates both
    // with the same string interpolation. A hand-edited template that lost its
    // placeholders leaves the fixed text instead of taking the settle down
    // (the poi spawner's rule for content).
    private static string FormatItem(
        bool starWon, CentoRaceRules rules, CentoMigliaEvent race, string carName)
    {
        string template = starWon ? rules.AwardItemName : rules.CertificateItemName;
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        object[] values = starWon
            ? [race.Year, carName ?? string.Empty]
            : [race.Week, race.Year, race.TierNumber];
        try
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture, template, values);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    private static double FiniteOr(double value, double fallback) =>
        double.IsFinite(value) ? value : fallback;

    private static double Cents(double amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
