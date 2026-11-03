using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The glove compartment's card rules (PORTPLAN 61, Unity
/// RewardCatalog.GetCapacity/CountCards + TodoRewardsScreenController's claim
/// guard): the capacity heals from the catalogue, slot and cash rewards are
/// consumed on claim, a card lands as a keepsake, and a full glovebox refuses
/// one instead of dropping it.
/// </summary>
public sealed class GloveboxTests
{
    private const string CardA = "DOUBLE NEXT TIP";
    private const string CardB = "NEXT FINE WAIVED";
    private const string HeatCard = "HEAT-FREE RIDES";

    private static readonly RewardTable Table = new(
        [
            new RewardCard(
                CardA, "Your next customer tip will be doubled",
                Effect: RewardCardEffect.DoubleNextTip, Amount: 2.0),
            new RewardCard(
                CardB, "Your next fine will be waived", Effect: RewardCardEffect.FineWaived),
            new RewardCard("+1 GLOVEBOX SLOT", "1 extra card fits", RewardCardKind.GloveboxSlot),
            new RewardCard("+500CR BONUS", "500 CR added to your bank account", RewardCardKind.CashBonus),
            new RewardCard(
                HeatCard, "No police the next 5 rides",
                Effect: RewardCardEffect.HeatFreeRides, Uses: 5),
        ],
        DefaultCapacity: 2,
        CashBonus: 500.0);

    private static DriverWallet Wallet(DriverState? saved = null) => new(saved ?? DriverState.Fresh);

    [Fact]
    public void HealsAMissingCapacityFromTheCatalogue()
    {
        // A save written before the reward cards existed carries 0 slots.
        DriverWallet wallet = Wallet(DriverState.Fresh with { GloveboxSlots = 0 });

        var glovebox = new Glovebox(Table, wallet);

        Assert.Equal(2, glovebox.Slots);
        Assert.Equal(2, wallet.GloveboxSlots);
    }

    [Fact]
    public void KeepsASavedCapacity()
    {
        DriverWallet wallet = Wallet(DriverState.Fresh with { GloveboxSlots = 4 });

        var glovebox = new Glovebox(Table, wallet);

        Assert.Equal(4, glovebox.Slots);
    }

    [Fact]
    public void CountsOnlyCarriedCards()
    {
        // A certificate is a keepsake too, but no catalogue row defines it.
        var trunk = new Trunk(
        [
            new TrunkItem(CardA, 0.0) { Quantity = 1 },
            new TrunkItem("Cento Miglia Certificate", 0.0) { Quantity = 1 },
        ]);
        var glovebox = new Glovebox(Table, Wallet());

        Assert.Equal(1, glovebox.Count(trunk));
        Assert.True(glovebox.HasRoom(trunk));
    }

    [Fact]
    public void SlotRewardRaisesTheCapacityAndStoresNothing()
    {
        DriverWallet wallet = Wallet();
        var trunk = new Trunk();
        var glovebox = new Glovebox(Table, wallet);

        RewardClaim claim = glovebox.Claim(Table.Cards[2], trunk);

        Assert.Equal(RewardClaimStatus.SlotAdded, claim.Status);
        Assert.True(claim.Applied);
        Assert.Equal(3, claim.GloveboxSlots);
        Assert.Equal(3, glovebox.Slots);
        Assert.Empty(trunk.Items);
    }

    [Fact]
    public void CashRewardAnswersTheTableBonusAndStoresNothing()
    {
        var trunk = new Trunk();
        var glovebox = new Glovebox(Table, Wallet());

        RewardClaim claim = glovebox.Claim(Table.Cards[3], trunk);

        Assert.Equal(RewardClaimStatus.BonusCredited, claim.Status);
        Assert.Equal(500.0, claim.Bonus);
        Assert.Empty(trunk.Items);
    }

    [Fact]
    public void CardRewardLandsInTheGlovecompartmentAsAKeepsake()
    {
        var trunk = new Trunk();
        var glovebox = new Glovebox(Table, Wallet());

        RewardClaim claim = glovebox.Claim(Table.Cards[0], trunk);

        Assert.Equal(RewardClaimStatus.Stored, claim.Status);
        Assert.True(trunk.Contains(CardA));
        // A card is a 0 L keepsake: a full car trunk never blocks one.
        Assert.Equal(0.0, trunk.Items[0].UnitVolume);
        Assert.Equal(0.0, trunk.UsedLitres);
        Assert.Equal(1, glovebox.Count(trunk));
    }

    [Fact]
    public void FullGloveboxRefusesACardAndMovesNothing()
    {
        var trunk = new Trunk([new TrunkItem(CardA, 0.0) { Quantity = 1 }]);
        var glovebox = new Glovebox(Table, Wallet(DriverState.Fresh with { GloveboxSlots = 1 }));

        RewardClaim claim = glovebox.Claim(Table.Cards[1], trunk);

        Assert.Equal(RewardClaimStatus.GloveboxFull, claim.Status);
        Assert.False(claim.Applied);
        Assert.False(trunk.Contains(CardB));
        Assert.Equal(1, glovebox.Count(trunk));
    }

    [Fact]
    public void AFullGloveboxStillTakesTheSlotReward()
    {
        // The slot reward is consumed on claim and never stored, so capacity is
        // no obstacle — Unity's slot branch runs before its capacity guard.
        var trunk = new Trunk([new TrunkItem(CardA, 0.0) { Quantity = 1 }]);
        var glovebox = new Glovebox(Table, Wallet(DriverState.Fresh with { GloveboxSlots = 1 }));

        RewardClaim claim = glovebox.Claim(Table.Cards[2], trunk);

        Assert.Equal(RewardClaimStatus.SlotAdded, claim.Status);
        Assert.Equal(2, glovebox.Slots);
        Assert.True(glovebox.HasRoom(trunk));
    }

    [Fact]
    public void CounterCardStartsOnItsFullUses()
    {
        // The "next 5 rides" card carries its countdown into the compartment:
        // the trunk page prints it and every completed job spends one.
        var trunk = new Trunk();
        var glovebox = new Glovebox(Table, Wallet());

        glovebox.Claim(Table.Find(HeatCard)!, trunk);

        TrunkItem card = Assert.Single(trunk.Items);
        Assert.Equal(1, card.Quantity);
        Assert.Equal(5, card.UsesLeft);
        Assert.Equal(5, card.UsesMax);
    }

    [Fact]
    public void RegisterCompletedJobCountsDownOneUseAndDropsTheSpentCard()
    {
        var trunk = new Trunk();
        var glovebox = new Glovebox(Table, Wallet());
        glovebox.Claim(Table.Find(HeatCard)!, trunk);

        // Four more jobs leave the card at one charge; the fifth spends it.
        for (int left = 4; left > 0; left--)
        {
            Assert.Equal(1, glovebox.RegisterCompletedJob(trunk));
            Assert.Equal(left, Assert.Single(trunk.Items).UsesLeft);
        }
        Assert.Equal(1, glovebox.RegisterCompletedJob(trunk));

        Assert.Empty(trunk.Items);
        Assert.Equal(0, glovebox.RegisterCompletedJob(trunk));
    }

    [Fact]
    public void RegisterCompletedJobLeavesAnUncountedCardAlone()
    {
        // A one-shot card is consumed by its own event (the next tip, the next
        // fine), never by a completed job, so the counter must not tick it.
        var trunk = new Trunk();
        var glovebox = new Glovebox(Table, Wallet());
        glovebox.Claim(Table.Cards[0], trunk);

        Assert.Equal(0, glovebox.RegisterCompletedJob(trunk));

        TrunkItem card = Assert.Single(trunk.Items);
        Assert.Equal(0, card.UsesLeft);
        Assert.Equal(0, card.UsesMax);
    }

    [Fact]
    public void HealCardUsesFillsACardSavedBeforeCountersExisted()
    {
        // A save written before the counter shipped holds the card with no
        // limit; boot heals it from the catalogue instead of showing nothing.
        var trunk = new Trunk([new TrunkItem(HeatCard, 0.0) { Quantity = 1 }]);
        var glovebox = new Glovebox(Table, Wallet());

        glovebox.HealCardUses(trunk);

        TrunkItem card = Assert.Single(trunk.Items);
        Assert.Equal(5, card.UsesLeft);
        Assert.Equal(5, card.UsesMax);
    }

    [Fact]
    public void HealCardUsesNeverRefillsAPartlySpentCard()
    {
        var trunk = new Trunk(
        [
            new TrunkItem(HeatCard, 0.0)
            {
                Quantity = 1,
                UsesLeft = 3,
                UsesMax = 5,
            },
        ]);
        var glovebox = new Glovebox(Table, Wallet());

        glovebox.HealCardUses(trunk);

        TrunkItem card = Assert.Single(trunk.Items);
        Assert.Equal(3, card.UsesLeft);
        Assert.Equal(5, card.UsesMax);
    }

    [Fact]
    public void WalletSnapshotCarriesTheCapacity()
    {
        DriverWallet wallet = Wallet();
        var glovebox = new Glovebox(Table, wallet);

        glovebox.Claim(Table.Cards[2], new Trunk());

        Assert.Equal(3, wallet.Snapshot().GloveboxSlots);
    }

    [Fact]
    public void TryConsumeSpendsExactlyOneCardOfTheEffect()
    {
        var trunk = new Trunk();
        var glovebox = new Glovebox(Table, Wallet());
        glovebox.Claim(Table.Find(CardB)!, trunk);

        Assert.True(glovebox.Carries(RewardCardEffect.FineWaived, trunk));

        Assert.True(glovebox.TryConsume(RewardCardEffect.FineWaived, trunk));
        Assert.False(glovebox.Carries(RewardCardEffect.FineWaived, trunk));
        // A second event with no card left falls back to the undiscounted rule.
        Assert.False(glovebox.TryConsume(RewardCardEffect.FineWaived, trunk));
        Assert.Empty(trunk.Items);
    }

    [Fact]
    public void TryConsumeLeavesTheOtherCardsAlone()
    {
        var trunk = new Trunk();
        var glovebox = new Glovebox(Table, Wallet());
        glovebox.Claim(Table.Find(CardA)!, trunk);
        glovebox.Claim(Table.Find(CardB)!, trunk);

        glovebox.TryConsume(RewardCardEffect.FineWaived, trunk);

        Assert.True(glovebox.Carries(RewardCardEffect.DoubleNextTip, trunk));
        Assert.False(glovebox.Carries(RewardCardEffect.FineWaived, trunk));
        Assert.Single(trunk.Items);
    }

    [Fact]
    public void StackedCardsCoverConsecutiveEventsOneAtATime()
    {
        var trunk = new Trunk();
        var glovebox = new Glovebox(Table, Wallet(DriverState.Fresh with { GloveboxSlots = 4 }));
        glovebox.Claim(Table.Find(CardB)!, trunk);
        glovebox.Claim(Table.Find(CardB)!, trunk);
        Assert.Equal(2, Assert.Single(trunk.Items).Quantity);

        Assert.True(glovebox.TryConsume(RewardCardEffect.FineWaived, trunk));
        Assert.True(glovebox.Carries(RewardCardEffect.FineWaived, trunk));
        Assert.True(glovebox.TryConsume(RewardCardEffect.FineWaived, trunk));
        Assert.False(glovebox.Carries(RewardCardEffect.FineWaived, trunk));
    }

    [Fact]
    public void HasChargesFollowsTheCounterCard()
    {
        var trunk = new Trunk();
        var glovebox = new Glovebox(Table, Wallet());
        Assert.False(glovebox.HasCharges(RewardCardEffect.HeatFreeRides, trunk));

        glovebox.Claim(Table.Find(HeatCard)!, trunk);

        Assert.True(glovebox.HasCharges(RewardCardEffect.HeatFreeRides, trunk));
        Assert.False(glovebox.HasCharges(RewardCardEffect.InvincibleRides, trunk));
    }

    [Fact]
    public void ReclaimingACounterCardExtendsTheCarriedOne()
    {
        // Unity's OnCardEarned: one card, more rides — never a second card in
        // the glovebox or a second count against the capacity.
        var trunk = new Trunk();
        var glovebox = new Glovebox(Table, Wallet());
        glovebox.Claim(Table.Find(HeatCard)!, trunk);
        glovebox.Claim(Table.Find(HeatCard)!, trunk);

        TrunkItem card = Assert.Single(trunk.Items);
        Assert.Equal(1, card.Quantity);
        Assert.Equal(10, card.UsesLeft);
        Assert.Equal(10, card.UsesMax);
        Assert.Equal(1, glovebox.Count(trunk));
    }
}
