using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The insurance broker page (PORTPLAN item 34, port of Unity
/// InsuranceScreen): the per-car cover rows and daily costs, the combined
/// total, the escrow collect block and the voice flow.</summary>
public sealed class InsuranceScreenTests
{
    private static readonly CarSpec Car = new(
        "ks_test_car", 2020, "Test", "Car", Passengers: 2, Class: 16, Price: 12000.0);

    private static readonly CarSpec Other = new(
        "ks_other", 2021, "Test", "Other", Passengers: 2, Class: 10, Price: 20000.0);

    private sealed record Rig(
        InsuranceScreenViewModel Vm, Garage Garage, DriverWallet Wallet, FakeClock Clock, VoiceBeat Beat,
        List<(string Key, bool Insured)> Toggles, List<double> Collected);

    private static Rig New(bool insured = false, bool insuredOther = false, double pending = 0.0,
        double cash = 500.0, bool withCars = true, Action? onBack = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        GarageState? saved = withCars
            ? new GarageState(Car.Key, [
                new OwnedCarState(Car.Key, 1, 0.0, insured),
                new OwnedCarState(Other.Key, 1, 0.0, insuredOther),
            ])
            : null;
        var garage = new Garage([Car, Other], saved);
        var wallet = new DriverWallet(new DriverState(cash, 0.0, PendingInsurance: pending));
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var toggles = new List<(string, bool)>();
        var collected = new List<double>();
        var vm = new InsuranceScreenViewModel(
            garage, wallet, InsuranceRules.Default, driverRating: 50.0, beat,
            new RelayCommand(() => onBack?.Invoke()),
            (key, insured2) => toggles.Add((key, insured2)),
            collected.Add);
        return new Rig(vm, garage, wallet, clock, beat, toggles, collected);
    }

    private static InsuranceCarRow Row(Rig rig, string key) => rig.Vm.Rows.Single(r => r.Key == key);

    [Fact]
    public void Rows_ListEveryCar_WithItsCoverAndDailyCost()
    {
        var rig = New(insured: true);

        InsuranceCarRow insured = Row(rig, Car.Key);
        Assert.Equal("REMOVE", insured.ToggleLabel);
        Assert.True(insured.IsInsured);
        Assert.Equal("Daily cost: CR3.45/day", insured.DailyLine); // 6.90 base − 50% rating

        InsuranceCarRow uncovered = Row(rig, Other.Key);
        Assert.Equal("ADD", uncovered.ToggleLabel);
        Assert.False(uncovered.IsInsured);
        Assert.Equal("Not insured", uncovered.DailyLine);
    }

    [Fact]
    public void Total_SumsOnlyTheInsuredCars()
    {
        Assert.Equal("Total daily cost: CR0.00/day", New().Vm.TotalLine);
        // Both insured: 3.45 + 5.75.
        Assert.Equal("Total daily cost: CR9.20/day", New(insured: true, insuredOther: true).Vm.TotalLine);
    }

    [Fact]
    public void Toggle_FlipsTheCover_AndBanks()
    {
        var rig = New();
        rig.Vm.ToggleCommand.Execute(Row(rig, Car.Key));

        Assert.True(rig.Garage.Find(Car.Key)!.HasInsurance);
        Assert.Equal("REMOVE", Row(rig, Car.Key).ToggleLabel);
        Assert.Equal((Car.Key, true), Assert.Single(rig.Toggles));
    }

    [Fact]
    public void Collect_MovesTheEscrowToCash()
    {
        var rig = New(pending: 10.0);
        Assert.True(rig.Vm.PendingVisible);

        rig.Vm.CollectCommand.Execute(null);

        Assert.Equal(510.0, rig.Wallet.Cash, 6);
        Assert.Equal(0.0, rig.Wallet.PendingInsurance);
        Assert.Equal(10.0, Assert.Single(rig.Collected));
        Assert.False(rig.Vm.PendingVisible);
    }

    [Fact]
    public void EmptyEscrow_CannotBeCollected()
    {
        var rig = New();
        Assert.False(rig.Vm.PendingVisible);
        rig.Vm.CollectCommand.Execute(null);
        Assert.Empty(rig.Collected);
    }

    [Fact]
    public void EmptyGarage_ShowsTheHint()
    {
        var rig = New(withCars: false);
        Assert.True(rig.Vm.IsEmpty);
        Assert.Empty(rig.Vm.Rows);
        Assert.Equal("No cars to insure yet.", rig.Vm.EmptyLine);
    }

    [Fact]
    public void Voice_BackGlowsThenNavigates()
    {
        bool navigated = false;
        var rig = New(onBack: () => navigated = true);

        rig.Vm.VoiceActions["BACK"].Invoke();
        Assert.True(rig.Vm.BackFlash);
        Assert.False(navigated);

        rig.Clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        rig.Beat.Poll();
        Assert.True(navigated);
        Assert.False(rig.Vm.BackFlash);
    }
}
