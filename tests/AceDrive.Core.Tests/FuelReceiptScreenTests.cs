using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The fuel receipt page (PORTPLAN item 37, port of Unity
/// FuelStationScreen's result block): the litres, the pump's CR/L, the bill,
/// the free-card note, the attendant's familiarity and the one way out.</summary>
public sealed class FuelReceiptScreenTests
{
    private static FuelReceiptScreenViewModel New(
        FuelReceipt receipt, DriverWallet? wallet = null, VoiceBeat? beat = null,
        Action? onContinue = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        return new FuelReceiptScreenViewModel(
            receipt,
            wallet ?? new DriverWallet(new DriverState(400.0, 0.0)),
            beat ?? new VoiceBeat(new FakeClock()),
            new RelayCommand(() => onContinue?.Invoke()));
    }

    private static FuelReceipt Receipt(
        double litres = 42.5, double price = 1.65, double total = 70.13,
        bool free = false, FriendshipResult? friendship = null) =>
        new(litres, price, total, free, friendship);

    [Fact]
    public void Lines_PrintTheFillThePriceAndTheBill()
    {
        var vm = New(Receipt());

        Assert.Equal("FUEL RECEIPT", vm.Title);
        Assert.Equal("Fuel added: 42.5 L", vm.LitresLine);
        Assert.Equal("Fuel price: €1.65/L", vm.PriceLine);
        Assert.Equal("Paid: €70.13", vm.PaidLine);
        Assert.False(vm.HasFree);
        Assert.False(vm.HasFriendship);
    }

    [Fact]
    public void AFreeFill_PrintsTheCardNote_AndNothingPaid()
    {
        var vm = New(Receipt(litres: 40.0, total: 0.0, free: true));

        Assert.True(vm.HasFree);
        Assert.Equal("Paid: €0.00", vm.PaidLine);
        Assert.Contains("Free Tank Refill", vm.FreeLine);
    }

    [Fact]
    public void AFill_RaisesTheAttendantsFamiliarity_AndHighlightsATier()
    {
        var vm = New(Receipt(friendship: new FriendshipResult("Rolf Brenner", 5, true)));

        Assert.True(vm.HasFriendship);
        Assert.Equal("New familiarity with Rolf: 5. Rolf is a friend.", vm.FriendshipLine);
        Assert.True(vm.FriendshipHighlight);
    }

    [Fact]
    public void TheWalletLine_ShowsTheDebtTheFillCanLeave()
    {
        var wallet = new DriverWallet(new DriverState(-12.5, 0.0));
        var vm = New(Receipt(total: 70.13), wallet);

        Assert.Equal("Wallet: €-12.50", vm.WalletLine);
    }

    [Fact]
    public void Voice_ContinueGlowsThenNavigates()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool navigated = false;
        var vm = New(Receipt(), beat: beat, onContinue: () => navigated = true);

        vm.VoiceActions["CONTINUE"].Invoke();
        Assert.True(vm.ContinueFlash);
        Assert.False(navigated); // the glow shows before the page flips

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.True(navigated);
        Assert.False(vm.ContinueFlash);
    }
}
