// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The reward catalogue behind a finished care-package run (PORTPLAN item 61,
// port of Unity RewardCatalog).
namespace AceDrive.Core;

/// <summary>
/// Every reward the community centre may offer, the glovebox slots a driver
/// starts with and the CR a bonus card banks. Loaded from
/// Data/reward_cards.json at startup; this Default is the test-shell fallback
/// (and the fixture the tests pin), not the source of truth.
/// </summary>
public sealed record RewardTable(
    IReadOnlyList<RewardCard> Cards,
    int DefaultCapacity = 2,
    double CashBonus = 500.0)
{
    public static readonly RewardTable Default = new(
        [
            new RewardCard(
                "FREE TANK REFILL", "One free tank refill",
                Effect: RewardCardEffect.FreeTankRefill),
            new RewardCard(
                "FREE CAR REPAIR", "One free car repair",
                Effect: RewardCardEffect.FreeCarRepair),
            new RewardCard(
                "+500CR BONUS", "500 CR added to your bank account", RewardCardKind.CashBonus),
            new RewardCard(
                "+1 GLOVEBOX SLOT", "1 extra card fits in the glovebox",
                RewardCardKind.GloveboxSlot),
            new RewardCard(
                "DELIVERY INSURANCE SHIELD",
                "You won't lose the invested money when failing a delivery",
                Effect: RewardCardEffect.DeliveryInsurance),
            new RewardCard(
                "ACCIDENT LOYALTY SHIELD",
                "You won't lose friendship with a customer following an accident",
                Effect: RewardCardEffect.AccidentLoyalty),
            new RewardCard(
                "HALF DAMAGE REDUCTION", "50% less damage from an accident",
                Effect: RewardCardEffect.HalfDamage, Amount: 0.5),
            new RewardCard(
                "JOURNALIST TEST RETAKE",
                "Retake a car journalist test of choice to get a better result",
                Effect: RewardCardEffect.JournalistRetake),
            new RewardCard(
                "DELIVERY PROFIT BOOST", "20% more profit on the next delivery",
                Effect: RewardCardEffect.DeliveryProfitBoost, Amount: 1.2),
            new RewardCard(
                "DOUBLE NEXT TIP", "Your next customer tip will be doubled",
                Effect: RewardCardEffect.DoubleNextTip, Amount: 2.0),
            new RewardCard(
                "NEXT FINE WAIVED", "Your next fine will be waived",
                Effect: RewardCardEffect.FineWaived),
            new RewardCard(
                "HEAT-FREE RIDES", "No police the next 5 rides",
                Effect: RewardCardEffect.HeatFreeRides, Uses: 5),
            new RewardCard(
                "INVINCIBLE RIDES", "No accidents for the next 5 rides",
                Effect: RewardCardEffect.InvincibleRides, Uses: 5),
            new RewardCard(
                "WORKER BONUS",
                "An employee making a delivery gives you double the return",
                Effect: RewardCardEffect.WorkerBonus, Amount: 2.0),
            new RewardCard(
                "DR PROTECTION",
                "Your driver rating is not reduced by an accident",
                Effect: RewardCardEffect.DriverRatingProtection),
        ]);

    /// <summary>
    /// The card with this stored title, or null when the name is not a reward
    /// card (a certificate, an award, a corrupt row).
    /// </summary>
    public RewardCard? Find(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        foreach (RewardCard card in Cards)
        {
            if (string.Equals(card.Title, title, StringComparison.Ordinal))
            {
                return card;
            }
        }

        return null;
    }

    /// <summary>
    /// The card that carries <paramref name="effect"/>, or null when no card in
    /// this catalogue defines it (a trimmed data file, a future effect the code
    /// already asks for). The amount a scaling effect applies lives here, so
    /// the flow never hardcodes a multiplier.
    /// </summary>
    public RewardCard? Find(RewardCardEffect effect)
    {
        if (effect == RewardCardEffect.None)
        {
            return null;
        }

        foreach (RewardCard card in Cards)
        {
            if (card.Effect == effect)
            {
                return card;
            }
        }

        return null;
    }

    /// <summary>
    /// True when the carried stack named <paramref name="name"/> is a glovebox
    /// card: the slot and cash rewards are consumed on claim and never stored,
    /// so they never match.
    /// </summary>
    public bool IsCard(string? name) => Find(name) is { IsCard: true };
}
