using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Delivery receipt page runs headless: every Unity line resolves
/// from the settled receipt and the resx, the rivals block follows the
/// standings backend, CONTINUE hands back to the host, and the hub blocks
/// (CredP, familiarity) hide when the run rested on no owned hub.</summary>
public sealed class DeliveryReceiptScreenTests
{
    private static (DeliveryReceiptScreenViewModel Vm, bool[] Continued, FakeRivals Rivals) Create(
        FakeRivals? rivals = null, DeliveryReceipt? receipt = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var continued = new bool[1];
        // The fixture run: 8.2 km, 49.00 wholesale out, 63.00 payout back.
        DeliveryReceipt settled = receipt ?? DeliveryReceiptBuilder.Build(
            8.2, paidForGoods: 49.00, receivedForGoods: 63.00,
            DeliveryFareTable.Default, carClass: 10, carRarity: 1,
            streetKnowledgePct: 100, currentXp: 0.0);
        // Empty wallet: the page lines must show the settle alone, not the
        // fresh-start seed (covered in DriverWalletTests).
        var wallet = new DriverWallet(new DriverState(0.0, 0.0));
        wallet.AddDelivery(settled);
        var fake = rivals ?? new FakeRivals();
        var vm = new DeliveryReceiptScreenViewModel(
            settled, wallet, beat,
            new RelayCommand(() => continued[0] = true), fake);
        return (vm, continued, fake);
    }

    [Fact]
    public void Receipt_OptsOutOfTheIdleHide()
    {
        // The breakdown waits for CONTINUE; the shell must not close the panel
        // 15 s into the read. The taxi receipt proves the shell rule end to
        // end; this pins the delivery page's opt-in.
        var (vm, _, _) = Create();

        Assert.IsAssignableFrom<IKeepPanelOpen>(vm);
    }

    [Fact]
    public void Lines_ShowTheSettledRun()
    {
        var (vm, _, _) = Create();
        Assert.Equal("Total Distance: 8.2 km", vm.DistanceLine);
        Assert.Equal(
            [new ReceiptLine("0.0–2.0 km @ €20.00/km: 2.0 km", "€40.00"),
             new ReceiptLine("2.0–5.0 km @ €17.00/km: 3.0 km", "€51.00"),
             new ReceiptLine("5.0–10.0 km @ €12.00/km: 3.2 km", "€38.40")],
            vm.TierLines);
        Assert.Equal(new ReceiptLine("Paid for goods", "€49.00"), vm.PaidLine);
        Assert.Equal(new ReceiptLine("Received for goods", "€63.00"), vm.ReceivedLine);
        Assert.Equal(new ReceiptLine("Fare", "€129.40"), vm.FareLine);
        Assert.Equal(new ReceiptLine("Total earned", "€143.40"), vm.TotalLine);
        Assert.Equal("Total CR: €143.40", vm.WalletLine);
        Assert.Equal("XP earned: 0.16", vm.XpLine);
        // Delivery's calculation line: the taxi terms at a fifth (/500).
        Assert.Equal(
            "XP: (8.2 km × class 10 × rarity 1) / 500 × street knowledge 100%",
            vm.XpCalcLine);
        Assert.Equal("Total XP: 0.16", vm.TotalXpLine);
    }

    [Fact]
    public void HublessRun_HidesFriendshipAndCredP()
    {
        var (vm, _, _) = Create();
        Assert.False(vm.HasFriendship);
        Assert.Equal(string.Empty, vm.FriendshipLine);
        Assert.False(vm.HasCredP);
    }

    [Fact]
    public void OwnedHubRun_ShowsFriendshipAndCredP()
    {
        var receipt = DeliveryReceiptBuilder.Build(
            8.2, 49.00, 63.00, DeliveryFareTable.Default, 10, 1, 100, 0.0,
            friendship: new FriendshipResult("Elena Marchetti", 1, false),
            credPEarned: 2, totalCredP: 2.0);
        var (vm, _, _) = Create(receipt: receipt);
        Assert.True(vm.HasFriendship);
        Assert.Equal("New familiarity with Elena: 1. Still a stranger though.", vm.FriendshipLine);
        Assert.False(vm.FriendshipHighlight);
        Assert.True(vm.HasCredP);
        Assert.Equal("Credibility Points earned: 2", vm.CredPEarnedLine);
        Assert.Equal("Total Credibility Points: 2", vm.TotalCredPLine);
    }

    [Fact]
    public void TierCross_HighlightsTheFamiliarityTeal()
    {
        var receipt = DeliveryReceiptBuilder.Build(
            8.2, 49.00, 63.00, DeliveryFareTable.Default, 10, 1, 100, 0.0,
            friendship: new FriendshipResult("Elena Marchetti", 6, true));
        var (vm, _, _) = Create(receipt: receipt);
        Assert.True(vm.FriendshipHighlight);
        // Unity FriendshipHelper bands: level 5+ already talks like a friend.
        Assert.Contains("friend", vm.FriendshipLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Continue_HandsBackToTheDeliveryBoard()
    {
        var (vm, continued, _) = Create();
        vm.ContinueCommand.Execute(null);
        Assert.True(continued[0]);
    }

    [Fact]
    public void Standings_PlaceholderThenLiveOnTheBoardEvent()
    {
        var rivals = new FakeRivals();
        rivals.Result = new RivalsResult(7, 41, 3, 10, false);
        var (vm, _, _) = Create(rivals: rivals);
        Assert.Equal("Your Global XP Rank is 7th of 41.", vm.Standings.Line);
        Assert.Equal("You gained 3 positions.", vm.Standings.DeltaLine);
        Assert.True(vm.Standings.Changed);
        Assert.Equal(1, vm.Standings.PulseSignal);

        // Unity unhooks its rivals handler in Hide(): after CONTINUE the
        // page no longer follows the board, so the leave-time lines stay.
        vm.ContinueCommand.Execute(null);
        rivals.Emit(new RivalsResult(1, 41, 6, 7, false));
        Assert.Equal("You gained 3 positions.", vm.Standings.DeltaLine);
    }
}
