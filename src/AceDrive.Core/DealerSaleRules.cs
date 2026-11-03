// The dealer's trade-in value (the sell half of PORTPLAN item 29). The mother
// game's dealer only buys; ACEDRIVE adds selling because its own dealer intro
// already promises it ("your car's resale value is directly affected by its
// engine condition and its Leaderboard Ranking"). The garage's displayed value
// is price × (1 − engine broken %); the sale keeps that condition rule and
// scales it by the car's 0..1 leaderboard ranking, so a top-ranked car keeps its
// full value and a bottom-ranked one is worth proportionally less. Pure data:
// the caller passes the readings, the rule does the arithmetic, no UI strings.
namespace AceDrive.Core;

public static class DealerSaleRules
{
    /// <summary>
    /// The CR a dealer pays for an owned car: price × (1 − broken%) × ranking.
    /// Non-finite or out-of-band readings clamp into 0..1, and a car with no
    /// usable catalogue price is worth nothing. Rounded to the cent, the same
    /// unit the wallet and the price line use.
    /// </summary>
    public static double Value(CarSpec? car, double engineBrokenPercent, double leaderboardRanking)
    {
        if (car is null || !double.IsFinite(car.Price) || car.Price <= 0.0)
        {
            return 0.0;
        }
        double health = 1.0 - Clamp01(engineBrokenPercent);
        double ranking = Clamp01(leaderboardRanking);
        return Math.Round(car.Price * health * ranking, 2, MidpointRounding.AwayFromZero);
    }

    // A torn or hand-edited reading must not invent money: clamp into the band
    // the two inputs are defined in, and treat a non-finite value as the low end.
    private static double Clamp01(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 0.0;
}
