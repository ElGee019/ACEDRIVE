using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The car dealer page (PORTPLAN 29): the week's floor, the picked car's
/// specs, the click gates on BUY and SELL, the purchase that hands a car to the
/// garage and the trade-in that hands one back.</summary>
public sealed class DealerScreenTests
{
    private static readonly CarSpec Mx5 = new(
        "ks_mazda_mx5_na", 1990, "Mazda", "mx5 na", Passengers: 1, Class: 9,
        Price: 12000.0, TrunkLitres: 150, Horsepower: 139, TopSpeedKmh: 185);

    private static readonly CarSpec Junior = new(
        "ks_alfa_romeo_junior", 2024, "Alfa Romeo", "junior", Passengers: 4, Class: 6,
        Price: 47500.0, TrunkLitres: 400, Horsepower: 280, TopSpeedKmh: 200);

    private static readonly CarSpec Hyper = new(
        "ks_ferrari_sf_25", 2025, "Ferrari", "sf 25", Passengers: 0, Class: 20,
        Price: 5000000.0, TrunkLitres: 0);

    private static (
        DealerScreenViewModel Vm, Garage Garage, DriverWallet Wallet,
        List<CarSpec> Bought, List<(CarSpec Car, double Paid)> Sold)
        Create(double cash = 50000.0, Garage? garage = null, int week = 3, VoiceBeat? beat = null)
    {
        // The stock is drawn with a week seed, so a test that needs to reach a
        // specific car pins the week that draws it (see PickInStock).
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var wallet = new DriverWallet(new DriverState(cash, 0.0));
        Garage g = garage ?? new Garage([Mx5, Junior, Hyper]);
        List<CarSpec> bought = [];
        List<(CarSpec Car, double Paid)> sold = [];
        beat ??= new VoiceBeat(new FakeClock());
        var vm = new DealerScreenViewModel(
            [Mx5, Junior, Hyper], g, wallet, week, new RelayCommand(() => { }),
            bought.Add, (car, paid) => sold.Add((car, paid)), beat);
        return (vm, g, wallet, bought, sold);
    }

    private static DealerRow RowFor(DealerScreenViewModel vm, string key) =>
        Assert.Single(vm.Rows, r => r.Key == key);

    [Fact]
    public void Floor_ShowsWhatThisWeekDrew()
    {
        var (vm, _, _, _, _) = Create();
        IReadOnlyList<CarSpec> expected = DealerStock.Build([Mx5, Junior, Hyper], new Garage([Mx5, Junior, Hyper]).Keys, 3);
        Assert.Equal(expected.Select(c => c.Key), vm.Rows.Select(r => r.Key));
        Assert.All(vm.Rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Title)));
    }

    [Fact]
    public void OwnedCars_AreNotOnTheFloor()
    {
        var garage = new Garage([Mx5, Junior, Hyper]);
        garage.Add(Mx5, week: 1);
        var (vm, _, _, _, _) = Create(garage: garage);
        Assert.DoesNotContain(Mx5.Key, vm.Rows.Select(r => r.Key));
    }

    [Fact]
    public void EverythingOwned_ShowsTheEmptyHint()
    {
        var garage = new Garage([Mx5, Junior, Hyper]);
        garage.Add(Mx5, week: 1);
        garage.Add(Junior, week: 1);
        garage.Add(Hyper, week: 1);
        var (vm, _, _, _, _) = Create(garage: garage);
        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Rows);
        Assert.False(string.IsNullOrWhiteSpace(vm.EmptyLine));
    }

    [Fact]
    public void BothButtons_OpenOnTheirVerbs()
    {
        // The bar always shows SELL and BUY; the gate copy only replaces those
        // verbs after a click, so neither button opens pre-disabled.
        var (vm, _, _, _, _) = Create(cash: 1000.0);
        Assert.Equal("SELL", vm.SellLabel);
        Assert.Equal("BUY", vm.BuyLabel);
        Assert.True(vm.SellCommand.CanExecute(null));
        Assert.True(vm.BuyCommand.CanExecute(null));
    }

    [Fact]
    public void Pick_ShowsTheSpecsAndThePrice()
    {
        var (vm, _, _, _, _) = Create(cash: 13000.0);
        Assert.Equal(vm.PickLine, vm.SelectedLine);
        Assert.Equal(string.Empty, vm.SelectedPriceLine);
        Assert.Equal(string.Empty, vm.SpecsLine);

        vm.SelectCommand.Execute(RowFor(vm, Mx5.Key));
        Assert.Equal("1990 MAZDA MX5 NA", vm.SelectedLine);
        Assert.Equal("CR12000", vm.SelectedPriceLine);
        Assert.Contains("139 hp", vm.SpecsLine, StringComparison.Ordinal);
        Assert.Contains("185 km/h", vm.SpecsLine, StringComparison.Ordinal);
        Assert.Equal("BUY", vm.BuyLabel);
    }

    [Fact]
    public void Buy_TooExpensive_ClickNamesTheGate()
    {
        // The button stays pressable and names the reason on the click; a stale
        // command state cannot spend.
        var (vm, _, wallet, bought, _) = Create(cash: 1000.0);
        vm.SelectCommand.Execute(RowFor(vm, Junior.Key));
        Assert.Equal("BUY", vm.BuyLabel);
        Assert.Equal("CR47500", vm.SelectedPriceLine);

        vm.BuyCommand.Execute(null);

        Assert.Equal("CAN'T AFFORD THIS CAR", vm.BuyLabel);
        Assert.Equal(1000.0, wallet.Cash);
        Assert.Empty(bought);
        // A new pick clears the reason and the verb comes back.
        vm.SelectCommand.Execute(RowFor(vm, Mx5.Key));
        Assert.Equal("BUY", vm.BuyLabel);
    }

    [Fact]
    public void Buy_MovesTheCashOwnsTheCarAndReportsIt()
    {
        var (vm, garage, wallet, bought, _) = Create(cash: 13000.0);
        vm.SelectCommand.Execute(RowFor(vm, Mx5.Key));
        vm.BuyCommand.Execute(null);

        Assert.Equal(1000.0, wallet.Cash);
        Assert.True(garage.Owns(Mx5.Key));
        Assert.Single(bought);
        Assert.Equal(Mx5.Key, bought[0].Key);
        // The bought car left the floor and the saldo line followed the cash.
        Assert.DoesNotContain(Mx5.Key, vm.Rows.Select(r => r.Key));
        Assert.Contains("CR1000.00", vm.CashLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Buy_KeepsTheCurrentCarUntilThePlayerChooses()
    {
        // Unity CarDealerScreen does not switch the driven car; the follow-up
        // screen does. The garage's first car stays current here.
        var garage = new Garage([Mx5, Junior, Hyper]);
        garage.Add(Junior, week: 1);
        var (vm, g, _, _, _) = Create(cash: 60000.0, garage: garage);
        vm.SelectCommand.Execute(RowFor(vm, Mx5.Key));
        vm.BuyCommand.Execute(null);
        Assert.True(g.Owns(Mx5.Key));
        Assert.Equal(Junior.Key, g.CurrentKey);
    }

    [Fact]
    public void NothingPicked_LeavesThePreviewOnTheHint()
    {
        var (vm, _, _, bought, _) = Create();
        Assert.Equal("BUY", vm.BuyLabel);
        vm.BuyCommand.Execute(null);
        Assert.Empty(bought);
        Assert.Equal("BUY", vm.BuyLabel);
    }

    [Fact]
    public void SaldoLine_OpensOnTheLiveWallet()
    {
        var (vm, _, _, _, _) = Create(cash: 1234.5);
        Assert.Contains("CR1234.50", vm.CashLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Sell_OpensTheGarageAndBuyReturnsToTheFloor()
    {
        var garage = new Garage([Mx5, Junior, Hyper]);
        garage.Add(Junior, week: 1);
        garage.Add(Mx5, week: 1);
        var (vm, _, _, _, _) = Create(garage: garage);

        Assert.Equal("SELL", vm.SellLabel);
        vm.SellCommand.Execute(null);

        Assert.True(vm.IsSelling);
        Assert.Equal("SELL", vm.SellLabel); // still the verb, no gate yet
        Assert.Equal("BUY", vm.BuyLabel);   // BUY stays visible and returns
        Assert.Equal(vm.Rows.Select(r => r.Key), garage.Cars.Select(c => c.Key));
        Assert.Equal("Pick the car you want to sell", vm.SelectedLine);

        vm.BuyCommand.Execute(null);
        Assert.False(vm.IsSelling);
        Assert.Equal("BUY", vm.BuyLabel);
        Assert.DoesNotContain(Mx5.Key, vm.Rows.Select(r => r.Key));
    }

    [Fact]
    public void Sell_OnlyCar_ClickNamesTheGateAndStaysOnTheFloor()
    {
        var garage = new Garage([Mx5, Junior, Hyper]);
        garage.Add(Mx5, week: 1);
        var (vm, _, _, _, sold) = Create(garage: garage);

        Assert.Equal("SELL", vm.SellLabel);
        vm.SellCommand.Execute(null);

        Assert.False(vm.IsSelling);
        Assert.Equal("CAN'T SELL YOUR ONLY CAR", vm.SellLabel);
        Assert.Empty(sold);
    }

    [Fact]
    public void Sell_NoCars_ClickNamesTheEmptyGate()
    {
        var (vm, _, _, _, sold) = Create(garage: new Garage([Mx5, Junior, Hyper]));

        vm.SellCommand.Execute(null);

        Assert.False(vm.IsSelling);
        Assert.Equal("NO CARS TO SELL", vm.SellLabel);
        Assert.Empty(sold);
    }

    [Fact]
    public void SellPaysTheConditionAndRankingValueAndRemovesTheCar()
    {
        var garage = new Garage([Mx5, Junior, Hyper]);
        garage.Add(Junior, week: 1);
        OwnedCar mazda = garage.Add(Mx5, week: 1);
        // 125k km on a small engine is 25% broken (250k red line), and a 0.5
        // leaderboard ranking halves what remains: 12000 × 0.75 × 0.5 = 4500.
        mazda.Drive(125000.0);
        mazda.SetLeaderboardRanking(0.5);
        var (vm, g, wallet, _, sold) = Create(cash: 100.0, garage: garage);

        vm.SellCommand.Execute(null); // open the garage
        vm.SelectCommand.Execute(RowFor(vm, Mx5.Key));
        Assert.Equal("Sell price: CR4500.00", vm.SelectedPriceLine);
        vm.SellCommand.Execute(null); // confirm

        Assert.Equal(4600.0, wallet.Cash);
        Assert.False(g.Owns(Mx5.Key));
        Assert.True(g.Owns(Junior.Key));
        Assert.Equal((Mx5.Key, 4500.0), (sold[0].Car.Key, sold[0].Paid));
        Assert.Contains("CR4600.00", vm.CashLine, StringComparison.Ordinal);
        // One car left: the page stays in sell mode, where a SELL click names
        // the gate even before the remaining car is picked.
        Assert.True(vm.IsSelling);
        Assert.Single(vm.Rows);
        vm.SellCommand.Execute(null);
        Assert.Equal("CAN'T SELL YOUR ONLY CAR", vm.SellLabel);
        vm.SelectCommand.Execute(RowFor(vm, Junior.Key)); // pick clears the reason
        Assert.Equal("SELL", vm.SellLabel);
        vm.SellCommand.Execute(null);
        Assert.Equal("CAN'T SELL YOUR ONLY CAR", vm.SellLabel);
    }

    [Fact]
    public void SellCurrentCar_HandsTheKeysToTheNextCar()
    {
        var garage = new Garage([Mx5, Junior, Hyper]);
        garage.Add(Junior, week: 1); // current
        garage.Add(Mx5, week: 1);
        var (vm, g, _, _, _) = Create(garage: garage);

        vm.SellCommand.Execute(null);
        vm.SelectCommand.Execute(RowFor(vm, Junior.Key));
        vm.SellCommand.Execute(null);

        Assert.False(g.Owns(Junior.Key));
        Assert.Equal(Mx5.Key, g.CurrentKey);
    }

    [Fact]
    public void Sell_KeepsTheDrawnFloor()
    {
        // The sold car was owned when the floor was drawn; a sale must not
        // redraw the floor (that would put the just-sold car back on it).
        var garage = new Garage([Mx5, Junior, Hyper]);
        garage.Add(Junior, week: 1);
        garage.Add(Mx5, week: 1);
        var (vm, _, _, _, _) = Create(garage: garage);
        string[] floor = [.. vm.Rows.Select(r => r.Key)];

        vm.SellCommand.Execute(null);
        vm.SelectCommand.Execute(RowFor(vm, Mx5.Key));
        vm.SellCommand.Execute(null);
        vm.BuyCommand.Execute(null); // back to the floor

        Assert.False(vm.IsSelling);
        Assert.Equal(floor, vm.Rows.Select(r => r.Key));
    }

    [Fact]
    public void Voice_CarNamePicksTheCar()
    {
        var (vm, _, _, _, _) = Create(cash: 13000.0);
        string firstTitle = vm.Rows[0].Title;
        Assert.True(vm.VoiceActions.ContainsKey(firstTitle));

        // The row's button text is the car name, so the name fires the pick:
        // the car is selected and its price shows below, exactly like a click.
        vm.VoiceActions[firstTitle].Invoke();

        Assert.Equal(firstTitle, vm.SelectedLine);
        Assert.StartsWith("CR", vm.SelectedPriceLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Voice_BuyTap_GlowsThenBuysAtTheBeatEnd()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var (vm, garage, wallet, bought, _) = Create(cash: 13000.0, beat: beat);
        vm.SelectCommand.Execute(RowFor(vm, Mx5.Key));

        vm.VoiceActions["BUY"].Invoke();
        Assert.True(vm.BuyFlash);
        Assert.Empty(bought); // still on this page while the beat runs
        Assert.Equal(13000.0, wallet.Cash);

        DrainBeat(clock, beat);
        Assert.True(garage.Owns(Mx5.Key));
        Assert.Single(bought);
        Assert.False(vm.BuyFlash); // no stale glow to re-show later
    }

    [Fact]
    public void Voice_SellTap_OpensThenTakesThePick()
    {
        var garage = new Garage([Mx5, Junior, Hyper]);
        garage.Add(Junior, week: 1);
        garage.Add(Mx5, week: 1);
        var (vm, g, wallet, _, _) = Create(cash: 0.0, garage: garage);

        vm.VoiceActions["SELL"].Invoke();
        Assert.True(vm.IsSelling);
        Assert.True(vm.VoiceActions.ContainsKey("BUY"));

        vm.VoiceActions[CarTitles.Of(Mx5)].Invoke();
        Assert.Equal(CarTitles.Of(Mx5), vm.SelectedLine);
        vm.VoiceActions["SELL"].Invoke();

        Assert.False(g.Owns(Mx5.Key));
        Assert.Equal(12000.0, wallet.Cash);
    }

    [Fact]
    public void Voice_BackTap_GlowsThenNavigatesAtTheBeatEnd()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool fired = false;
        var wallet = new DriverWallet(new DriverState(13000.0, 0.0));
        var vm = new DealerScreenViewModel(
            [Mx5, Junior, Hyper], new Garage([Mx5, Junior, Hyper]), wallet, 3,
            new RelayCommand(() => fired = true), _ => { }, (_, _) => { }, beat);

        vm.VoiceActions["BACK"].Invoke();
        Assert.True(vm.BackFlash);
        Assert.False(fired); // the page waits out the beat

        DrainBeat(clock, beat);
        Assert.True(fired);
        Assert.False(vm.BackFlash);
    }

    // What MainViewModel.Poll does at 10 Hz: wall time passes, the beat drains.
    private static void DrainBeat(FakeClock clock, VoiceBeat beat)
    {
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
    }
}
