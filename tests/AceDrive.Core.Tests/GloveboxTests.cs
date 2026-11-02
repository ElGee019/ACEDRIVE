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

    private static readonly RewardTable Table = new(
        [
            new RewardCard(CardA, "Your next customer tip will be doubled"),
            new RewardCard(CardB, "Your next fine will be waived"),
            new RewardCard("+1 GLOVEBOX SLOT", "1 extra card fits", RewardCardKind.GloveboxSlot),
            new RewardCard("+500CR BONUS", "500 CR added to your cash", RewardCardKind.CashBonus),
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

        Assert.Equal(RewardClaimStatus.CashCredited, claim.Status);
        Assert.Equal(500.0, claim.Cash);
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
    public void WalletSnapshotCarriesTheCapacity()
    {
        DriverWallet wallet = Wallet();
        var glovebox = new Glovebox(Table, wallet);

        glovebox.Claim(Table.Cards[2], new Trunk());

        Assert.Equal(3, wallet.Snapshot().GloveboxSlots);
    }
}
