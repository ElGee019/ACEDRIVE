using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Taxi receipt page runs headless: every Unity line resolves from
/// the settled receipt and the resx, CONTINUE hands back to the host.</summary>
public sealed class TaxiReceiptScreenTests
{
    private static (TaxiReceiptScreenViewModel Vm, bool[] Continued) Create()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var continued = new bool[1];
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, capacity: 1, carClass: 1, carRarity: 1,
            streetKnowledgePct: 100, tipEligible: true, fines: 1,
            FareTable.Default, currentXp: 0.0);
        var wallet = new DriverWallet();
        wallet.AddReceipt(receipt);
        var shift = new TaxiShift();
        shift.RecordCompleted(
            new TaxiJobState { Phase = JobPhase.Complete, Fare = 53.15, Tip = 10.63, Fine = 50.0 },
            clock.UtcNow);
        var vm = new TaxiReceiptScreenViewModel(
            receipt, "Solo Sohn", wallet, shift, beat,
            new RelayCommand(() => continued[0] = true));
        return (vm, continued);
    }

    [Fact]
    public void Lines_ShowTheSettledRide()
    {
        var (vm, _) = Create();
        Assert.Equal("Distance: 8.2 km", vm.DistanceLine);
        Assert.Equal("Customers: 1. Solo Sohn", vm.CustomerLine);
        Assert.Equal(
            ["0.0–2.0 km @ €6.67/km: 2.0 km = €13.34",
             "2.0–5.0 km @ €5.67/km: 3.0 km = €17.01",
             "5.0–10.0 km @ €4.00/km: 3.2 km = €12.80"],
            vm.TierLines);
        Assert.Equal("Forfait: €10.00", vm.ForfaitLine);
        Assert.Equal("Fare: 1 × €43.15 = €43.15", vm.FareLine);
        Assert.True(vm.HasTip);
        Assert.Equal("Customer tip: €10.63", vm.TipLine);
        Assert.True(vm.HasFine);
        Assert.Equal("Speed fine: −€50.00", vm.FineLine);
        Assert.Equal("Total earned: €13.78", vm.TotalLine);
        Assert.Equal("Wallet: €13.78", vm.WalletLine);
        Assert.Equal("Shift: 1 jobs · €13.78", vm.ShiftLine);
        Assert.Equal("XP earned: 0.08", vm.XpLine);
        Assert.Equal("XP: (8.2 km × class 1 × rarity 1) / 100 × street knowledge 100%", vm.XpCalcLine);
        Assert.Equal("Total XP: 0.08", vm.TotalXpLine);
    }

    [Fact]
    public void Continue_HandsBackToHost()
    {
        var (vm, continued) = Create();
        vm.ContinueCommand.Execute(null);
        Assert.True(continued[0]);
    }

    [Fact]
    public void VoiceContinue_AnswersToItsLabel()
    {
        var (vm, continued) = Create();
        Assert.True(vm.VoiceActions.TryGetValue(Strings.Get("ReceiptContinue"), out Action? act));
        act!();
        // Navigating taps wait out the shared beat; the click path acts now.
        Assert.False(continued[0]);
        vm.ContinueCommand.Execute(null);
        Assert.True(continued[0]);
    }

    [Fact]
    public void WithoutFriendship_BoxHidden()
    {
        var (vm, _) = Create();
        Assert.False(vm.HasFriendship);
        Assert.Equal(string.Empty, vm.FriendshipLine);
        Assert.False(vm.FriendshipHighlight);
    }

    [Fact]
    public void WithFriendship_ShowsTierSentence()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, capacity: 1, carClass: 1, carRarity: 1,
            streetKnowledgePct: 100, tipEligible: true, fines: 1,
            FareTable.Default, currentXp: 0.0,
            new FriendshipResult("Finn Marsh", 5, true));
        var vm = new TaxiReceiptScreenViewModel(
            receipt, "Finn Marsh", new DriverWallet(), new TaxiShift(), beat,
            new RelayCommand(() => { }));
        Assert.True(vm.HasFriendship);
        Assert.Equal("New familiarity with Finn: 5. Finn is a friend.", vm.FriendshipLine);
        Assert.True(vm.FriendshipHighlight);
    }
}
