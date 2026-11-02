using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The care-package reward picker (PORTPLAN 61, Unity CANVASREWARDS): the two
/// rewards on offer, one claim that lands in the glove compartment, a full
/// glovebox that keeps the choice open, and CONTINUE's shared voice beat. The
/// page is driven the way the flow builds it, over the reward desk that owns the
/// draw and the bank.
/// </summary>
public sealed class RewardPickerScreenViewModelTests
{
    private const string TipCard = "DOUBLE NEXT TIP";
    private const string FineCard = "NEXT FINE WAIVED";
    private const string SlotCard = "+1 GLOVEBOX SLOT";
    private const string CashCard = "+500CR BONUS";

    // The two stored-card rewards: the catalogue a suite that does not care
    // about the slot or cash branches pins, so both buttons are plain cards.
    private static readonly RewardCard[] StoredCards =
    [
        new RewardCard(TipCard, "Your next customer tip will be doubled"),
        new RewardCard(FineCard, "Your next fine will be waived"),
    ];

    // The page copy follows the UI culture; tests pin en-US.
    public RewardPickerScreenViewModelTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    private sealed record Page(
        RewardPickerScreenViewModel Vm, CarePackageReward Rewards, DriverWallet Wallet,
        Trunk Trunk, VoiceBeat Beat, FakeClock Clock, List<string> Events);

    private static Page Create(
        int capacity = 2, IReadOnlyList<TrunkItem>? carried = null,
        IReadOnlyList<RewardCard>? cards = null)
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
        var vm = new RewardPickerScreenViewModel(
            offer, rewards, beat, new RelayCommand(() => events.Add("continue")));
        return new Page(vm, rewards, wallet, trunk, beat, clock, events);
    }

    [Fact]
    public void Open_OffersBothCardsAndTheGloveboxReading()
    {
        Page page = Create();

        Assert.True(page.Vm.OfferOpen);
        Assert.Equal(2, page.Vm.Cards.Count);
        Assert.NotEqual(page.Vm.First.Title, page.Vm.Second.Title);
        Assert.Contains("0/2", page.Vm.GloveboxLine, StringComparison.Ordinal);
        Assert.False(page.Vm.HasNotice);
        Assert.True(page.Vm.VoiceActions.ContainsKey(TipCard));
        Assert.True(page.Vm.VoiceActions.ContainsKey(FineCard));
        Assert.True(page.Vm.VoiceActions.ContainsKey("CONTINUE"));
    }

    [Fact]
    public void Claim_StoresTheCardClosesTheOfferAndBanksTheRun()
    {
        Page page = Create();

        page.Vm.ClaimCommand.Execute(page.Vm.First);

        Assert.False(page.Vm.OfferOpen);
        Assert.True(page.Vm.HasNotice);
        Assert.Contains(page.Vm.First.Title, page.Vm.Notice, StringComparison.Ordinal);
        Assert.True(page.Trunk.Contains(page.Vm.First.Title));
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

        page.Vm.ClaimCommand.Execute(page.Vm.First);
        page.Vm.ClaimCommand.Execute(page.Vm.Second);

        Assert.Equal(1, page.Rewards.CardCount);
        Assert.Single(page.Events);
    }

    [Fact]
    public void FullGloveboxKeepsBothRewardsUpAndSavesNothing()
    {
        // One slot, already taken by a carried card.
        Page page = Create(
            capacity: 1, carried: [new TrunkItem(TipCard, 0.0) { Quantity = 1 }]);

        page.Vm.ClaimCommand.Execute(page.Vm.First);

        Assert.True(page.Vm.OfferOpen);
        Assert.Contains("full", page.Vm.Notice, StringComparison.OrdinalIgnoreCase);
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

        page.Vm.ClaimCommand.Execute(page.Vm.First);

        Assert.False(page.Vm.OfferOpen);
        Assert.False(page.Trunk.Contains(SlotCard));
        Assert.Equal(3, page.Rewards.Slots);
        Assert.Contains("3", page.Vm.Notice, StringComparison.Ordinal);
        Assert.Contains("0/3", page.Vm.GloveboxLine, StringComparison.Ordinal);
    }

    [Fact]
    public void CashClaim_PaysTheGiftIntoTheWallet()
    {
        Page page = Create(cards:
        [
            new RewardCard(CashCard, "500 CR added to your cash", RewardCardKind.CashBonus),
            new RewardCard(TipCard, "Your next customer tip will be doubled"),
        ]);

        page.Vm.ClaimCommand.Execute(page.Vm.First);

        Assert.False(page.Vm.OfferOpen);
        // Unity's bonus lands in the spendable balance, never as a stored card.
        Assert.Equal(500.0, page.Wallet.Cash);
        Assert.False(page.Trunk.Contains(CashCard));
        Assert.Contains("500", page.Vm.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public void VoiceClaim_ClaimsTheCardItNamed()
    {
        Page page = Create();

        page.Vm.VoiceActions[page.Vm.Second.Title].Invoke();

        Assert.False(page.Vm.OfferOpen);
        Assert.True(page.Trunk.Contains(page.Vm.Second.Title));
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
