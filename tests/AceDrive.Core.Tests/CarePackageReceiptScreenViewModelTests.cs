using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The care-package delivered page (PORTPLAN items 21 and 61, Unity's completion
/// edge): the receipt numbers plus the two-reward choice the community centre
/// offers, one claim that lands in the glove compartment, a full glovebox that
/// keeps the choice open, and CONTINUE's shared voice beat. The page is driven
/// the way the flow builds it, over the reward desk that owns the draw and the
/// bank.
/// </summary>
public sealed class CarePackageReceiptScreenViewModelTests
{
    private const string TipCard = "DOUBLE NEXT TIP";
    private const string FineCard = "NEXT FINE WAIVED";
    private const string SlotCard = "+1 GLOVEBOX SLOT";
    private const string CashCard = "+500CR BONUS";

    // The two stored-card rewards: the catalogue a suite that does not care
    // about the slot or cash branches pins, so both cards are plain cards.
    private static readonly RewardCard[] StoredCards =
    [
        new RewardCard(TipCard, "Your next customer tip will be doubled"),
        new RewardCard(FineCard, "Your next fine will be waived"),
    ];

    // The page copy follows the UI culture; tests pin en-US.
    public CarePackageReceiptScreenViewModelTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    private sealed record Page(
        CarePackageReceiptScreenViewModel Vm, CarePackageReward Rewards, DriverWallet Wallet,
        Trunk Trunk, VoiceBeat Beat, FakeClock Clock, List<string> Events);

    private static Page Create(
        int capacity = 2, IReadOnlyList<TrunkItem>? carried = null,
        IReadOnlyList<RewardCard>? cards = null, bool withReward = true)
    {
        var wallet = new DriverWallet(DriverState.Fresh with { GloveboxSlots = capacity });
        var trunk = new Trunk(carried ?? []);
        var table = new RewardTable(
            cards ?? StoredCards, DefaultCapacity: capacity, CashBonus: 500.0);
        var glovebox = new Glovebox(table, wallet);
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var events = new List<string>();
        var rewards = new CarePackageReward(
            table, glovebox, trunk, wallet, new Random(1),
            () => events.Add("saved"), new NullLogger());
        var offer = Assert.IsType<RewardOffer>(rewards.Offer());
        var receipt = new CarePackageReceipt(5, 0, ["Max's Grocery Store"]);
        var vm = new CarePackageReceiptScreenViewModel(
            receipt,
            withReward ? new RewardChooser(offer, rewards) : null,
            beat, new RelayCommand(() => events.Add("continue")));
        return new Page(vm, rewards, wallet, trunk, beat, clock, events);
    }

    [Fact]
    public void DeliveredPage_PrintsTheRunAndOffersBothCards()
    {
        Page page = Create();

        Assert.Contains("5", page.Vm.DeliveredLine, StringComparison.Ordinal);
        Assert.True(page.Vm.HasStores);
        Assert.Contains("Max's Grocery Store", page.Vm.StoresLine, StringComparison.Ordinal);
        Assert.Equal("Every care kit in town has been delivered.", page.Vm.RemainingLine);

        Assert.True(page.Vm.HasReward);
        Assert.True(page.Vm.RewardOpen);
        Assert.Equal(2, page.Vm.RewardCards.Count);
        Assert.NotEqual(page.Vm.RewardCards[0].Title, page.Vm.RewardCards[1].Title);
        Assert.Contains("0/2", page.Vm.GloveboxLine, StringComparison.Ordinal);
        Assert.False(page.Vm.HasRewardNotice);
        Assert.True(page.Vm.VoiceActions.ContainsKey(TipCard));
        Assert.True(page.Vm.VoiceActions.ContainsKey(FineCard));
        Assert.True(page.Vm.VoiceActions.ContainsKey("CONTINUE"));
    }

    [Fact]
    public void DeliveredPage_WithoutACataloguePair_PrintsTheReceiptOnly()
    {
        Page page = Create(withReward: false);

        Assert.False(page.Vm.HasReward);
        Assert.False(page.Vm.RewardOpen);
        Assert.Empty(page.Vm.RewardCards);
        Assert.Empty(page.Vm.GloveboxLine);
        Assert.False(page.Vm.HasRewardNotice);
        Assert.Single(page.Vm.VoiceActions);
        Assert.True(page.Vm.VoiceActions.ContainsKey("CONTINUE"));
    }

    [Fact]
    public void Claim_StoresTheCardClosesTheOfferAndBanksTheRun()
    {
        Page page = Create();

        page.Vm.ClaimCommand.Execute(page.Vm.RewardCards[0]);

        Assert.False(page.Vm.RewardOpen);
        Assert.True(page.Vm.HasRewardNotice);
        Assert.Contains(page.Vm.RewardCards[0].Title, page.Vm.RewardNotice, StringComparison.Ordinal);
        Assert.True(page.Trunk.Contains(page.Vm.RewardCards[0].Title));
        Assert.Equal(["saved"], page.Events);
        // A claimed page can only hear CONTINUE: the rewards are gone.
        Assert.Single(page.Vm.VoiceActions);
        Assert.True(page.Vm.VoiceActions.ContainsKey("CONTINUE"));
        Assert.Contains("1/2", page.Vm.GloveboxLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Claim_TwiceIsIgnoredSoOneRunPaysOneReward()
    {
        Page page = Create();

        page.Vm.ClaimCommand.Execute(page.Vm.RewardCards[0]);
        page.Vm.ClaimCommand.Execute(page.Vm.RewardCards[1]);

        Assert.Equal(1, page.Rewards.CardCount);
        Assert.Single(page.Events);
    }

    [Fact]
    public void FullGloveboxKeepsBothRewardsUpAndSavesNothing()
    {
        // One slot, already taken by a carried card.
        Page page = Create(
            capacity: 1, carried: [new TrunkItem(TipCard, 0.0) { Quantity = 1 }]);

        page.Vm.ClaimCommand.Execute(page.Vm.RewardCards[0]);

        Assert.True(page.Vm.RewardOpen);
        Assert.Contains("full", page.Vm.RewardNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, page.Rewards.CardCount);
        Assert.Empty(page.Events);
        // Both rewards still answer: the player can take the other one.
        Assert.True(page.Vm.VoiceActions.ContainsKey(TipCard));
    }

    [Fact]
    public void SlotClaim_RaisesTheReadingAndPrintsIt()
    {
        // A slot card on the left, so the claim can be the page's first card.
        Page page = Create(cards:
        [
            new RewardCard(SlotCard, "1 extra card fits", RewardCardKind.GloveboxSlot),
            new RewardCard(TipCard, "Your next customer tip will be doubled"),
        ]);

        page.Vm.ClaimCommand.Execute(page.Vm.RewardCards[0]);

        Assert.False(page.Vm.RewardOpen);
        Assert.False(page.Trunk.Contains(SlotCard));
        Assert.Equal(3, page.Rewards.Slots);
        Assert.Contains("3", page.Vm.RewardNotice, StringComparison.Ordinal);
        Assert.Contains("0/3", page.Vm.GloveboxLine, StringComparison.Ordinal);
    }

    [Fact]
    public void CashClaim_BanksTheBonusAndLeavesTheCashAlone()
    {
        Page page = Create(cards:
        [
            new RewardCard(CashCard, "500 CR added to your bank account", RewardCardKind.CashBonus),
            new RewardCard(TipCard, "Your next customer tip will be doubled"),
        ]);

        page.Vm.ClaimCommand.Execute(page.Vm.RewardCards[0]);

        Assert.False(page.Vm.RewardOpen);
        // Unity's bonus is a bank transfer (bankAmount += 500), never cash in
        // hand and never a stored card.
        Assert.Equal(1500.0, page.Wallet.BankBalance);
        Assert.Equal(0.0, page.Wallet.Cash);
        Assert.False(page.Trunk.Contains(CashCard));
        Assert.Contains("500", page.Vm.RewardNotice, StringComparison.Ordinal);
        Assert.Contains("bank account", page.Vm.RewardNotice, StringComparison.Ordinal);
    }

    [Fact]
    public void VoiceClaim_ClaimsTheCardItNamed()
    {
        Page page = Create();

        page.Vm.VoiceActions[page.Vm.RewardCards[1].Title].Invoke();

        Assert.False(page.Vm.RewardOpen);
        Assert.True(page.Trunk.Contains(page.Vm.RewardCards[1].Title));
    }

    [Fact]
    public void VoiceContinue_GlowsThenLeavesAtTheEndOfTheBeat()
    {
        Page page = Create();

        page.Vm.VoiceActions["CONTINUE"].Invoke();

        Assert.True(page.Vm.ContinueFlash);
        Assert.Empty(page.Events);
        // What the 10 Hz poll does: wall time passes and the beat drains.
        page.Clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        page.Beat.Poll();
        Assert.Equal(["continue"], page.Events);
    }
}
