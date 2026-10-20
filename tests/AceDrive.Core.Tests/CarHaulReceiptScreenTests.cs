using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Car-haul receipt page runs headless: every Unity line resolves
/// from the settled receipt and the resx, CONTINUE hands back to the host,
/// and there is no XP or standings block (Unity settles the haul's cash
/// alone).</summary>
public sealed class CarHaulReceiptScreenTests
{
    private static (CarHaulReceiptScreenViewModel Vm, bool[] Continued, DriverWallet Wallet) Create(
        CarHaulReceipt? receipt = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var continued = new bool[1];
        // The fixture haul: class-15 car (tier 6), rating 50, 30 minutes.
        CarHaulReceipt settled = receipt ?? CarHaulReceiptBuilder.Build(
            "Test Hatch", carClass: 15, driverRating: 50, driveMinutes: 30);
        var wallet = new DriverWallet();
        wallet.AddHaul(settled);
        var vm = new CarHaulReceiptScreenViewModel(
            settled, wallet, beat,
            new RelayCommand(() => continued[0] = true));
        return (vm, continued, wallet);
    }

    [Fact]
    public void Lines_ShowTheSettledHaul()
    {
        var (vm, _, wallet) = Create();
        Assert.Equal("Test Hatch", vm.CarLine);
        Assert.Equal("The drive took you 0 hours and 30 minutes", vm.TimeLine);
        Assert.Equal(new ReceiptLine("You earned", "€74.70"), vm.EarnedLine);
        Assert.Equal("Your Driver Rating: 50.0", vm.DriverRatingLine);
        Assert.Equal("The current class is Tier 6", vm.TierLine);
        Assert.Equal("Resulting in a rate of €2.49/min", vm.RateLine);
        Assert.Equal("Wallet: €74.70", vm.WalletLine);
        Assert.Equal(74.70, wallet.Cash);
    }

    [Fact]
    public void WholeHours_WordChoicePluralisesOnCount()
    {
        var (vm, _, _) = Create(ReceiptFor(60));
        Assert.Equal("The drive took you 1 hour and 0 minutes", vm.TimeLine);
    }

    private static CarHaulReceipt ReceiptFor(int minutes) =>
        CarHaulReceiptBuilder.Build(
            "Test Hatch", carClass: 15, driverRating: 50, driveMinutes: minutes);

    [Fact]
    public void NoXpBlock_TheHaulHasNoXpTail()
    {
        var (vm, _, _) = Create();
        // The page simply has no XP or standings members to render: the
        // absence is enforced by the API shape, not by hidden rows.
        Assert.DoesNotContain(vm.GetType().GetProperties(), p => p.Name.Contains("Xp", StringComparison.Ordinal));
        Assert.DoesNotContain(vm.GetType().GetProperties(), p => p.Name.Contains("Standings", StringComparison.Ordinal));
    }

    [Fact]
    public void Continue_HandsBackToHost()
    {
        var (vm, continued, _) = Create();
        vm.ContinueCommand.Execute(null);
        Assert.True(continued[0]);
    }

    [Fact]
    public void VoiceContinue_AnswersToItsLabel()
    {
        var (vm, continued, _) = Create();
        Assert.True(vm.VoiceActions.TryGetValue(Strings.Get("ReceiptContinue"), out Action? act));
        act!();
        // Navigating taps wait out the shared beat; the click path acts now.
        Assert.False(continued[0]);
        vm.ContinueCommand.Execute(null);
        Assert.True(continued[0]);
    }

    [Fact]
    public void WithoutDealerOwner_BoxHidden()
    {
        var (vm, _, _) = Create();
        Assert.False(vm.HasFriendship);
        Assert.Equal(string.Empty, vm.FriendshipLine);
        Assert.False(vm.FriendshipHighlight);
    }

    [Fact]
    public void WithDealerOwner_ShowsFamiliaritySentence()
    {
        CarHaulReceipt receipt = CarHaulReceiptBuilder.Build(
            "Test Hatch", carClass: 15, driverRating: 50, driveMinutes: 30,
            dealerOwnerName: "Elena Marchetti", dealerFriendshipLevel: 2,
            dealerFriendshipTierIncreased: true);
        var (vm, _, _) = Create(receipt);
        Assert.True(vm.HasFriendship);
        Assert.Equal("New familiarity with Elena: 2. Still a stranger though.", vm.FriendshipLine);
        Assert.True(vm.FriendshipHighlight);
    }
}
