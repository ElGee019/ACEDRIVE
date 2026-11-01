using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The insurance broker page (PORTPLAN item 34, port of Unity InsuranceScreen):
/// the landing page with MANAGE and COLLECT, the per-car cover list with the
/// selected car's price and premium, the escrow block behind COLLECT, and BACK
/// closing a block before it leaves the shop. Copy is asserted through the resx
/// so a translator's edit is what the page shows.
/// </summary>
public sealed class InsuranceScreenTests
{
    private const string StoreName = "Marco's Insurance";

    private static readonly CarSpec Car = new(
        "ks_test_car", 2020, "Test", "Car", Passengers: 2, Class: 16, Price: 12000.0);

    private static readonly CarSpec Other = new(
        "ks_other", 2021, "Test", "Other", Passengers: 2, Class: 10, Price: 20000.0);

    private sealed record Rig(
        InsuranceScreenViewModel Vm, Garage Garage, DriverWallet Wallet, FakeClock Clock,
        VoiceBeat Beat, List<(string Key, bool Insured)> Toggles, List<double> Collected);

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
            garage, wallet, InsuranceRules.Default, driverRating: 50.0, StoreName, beat,
            new RelayCommand(() => onBack?.Invoke()),
            (key, insured2) => toggles.Add((key, insured2)),
            collected.Add);
        return new Rig(vm, garage, wallet, clock, beat, toggles, collected);
    }

    private static InsuranceCarRow Row(Rig rig, string key) => rig.Vm.Rows.Single(r => r.Key == key);

    [Fact]
    public void Landing_OpensOnTheBrokersName_WithTheTwoVerbs()
    {
        var rig = New();

        Assert.True(rig.Vm.IsLandingView);
        Assert.False(rig.Vm.IsManageView);
        Assert.False(rig.Vm.IsCollectView);
        // Unity's "_textStore": the POI's own name over the page.
        Assert.Equal("MARCO'S INSURANCE", rig.Vm.StoreLine);
    }

    [Fact]
    public void Manage_ListsEveryCar_AndOpensOnTheFirstRow()
    {
        var rig = New(insured: true);

        rig.Vm.ManageCommand.Execute(null);

        Assert.True(rig.Vm.IsManageView);
        Assert.False(rig.Vm.IsLandingView);
        Assert.Equal(2, rig.Vm.Rows.Count);
        // Unity opens the list on its first row and paints that car's details.
        InsuranceCarRow first = rig.Vm.Rows[0];
        Assert.True(first.IsSelected);
        Assert.False(first.IsInsured);
        Assert.Equal("Car newprice: CR20000.00", rig.Vm.ValueCarLine);
        Assert.Equal("Daily cost for this car: CR0.00/day", rig.Vm.DailyCostLine);
        // The covered car reads REMOVE and is what the total sums.
        InsuranceCarRow covered = Row(rig, Car.Key);
        Assert.True(covered.IsInsured);
        Assert.False(covered.IsSelected);
        Assert.Equal("REMOVE", covered.ToggleLabel);
        Assert.Equal("ADD", first.ToggleLabel);
        Assert.Equal("Total daily cost: CR3.45/day", rig.Vm.TotalLine);
    }

    [Fact]
    public void Select_PaintsTheTappedCarsPriceAndPremium()
    {
        var rig = New(insured: true);
        rig.Vm.ManageCommand.Execute(null);

        rig.Vm.SelectCommand.Execute(Row(rig, Other.Key));

        Assert.True(Row(rig, Other.Key).IsSelected);
        Assert.False(Row(rig, Car.Key).IsSelected);
        Assert.Equal("Car newprice: CR20000.00", rig.Vm.ValueCarLine);
        // Uncovered: Unity prints the zero daily cost and no discount breakdown.
        Assert.Equal("Daily cost for this car: CR0.00/day", rig.Vm.DailyCostLine);
    }

    [Fact]
    public void Toggle_FlipsTheCover_AndBanks()
    {
        var rig = New();
        rig.Vm.ManageCommand.Execute(null);

        rig.Vm.ToggleCommand.Execute(Row(rig, Car.Key));
        Assert.True(rig.Garage.Find(Car.Key)!.HasInsurance);
        Assert.Equal("REMOVE", Row(rig, Car.Key).ToggleLabel);
        Assert.Equal(
            "Daily cost for this car: CR3.45/day (CR6.90 − 50% driver rating)",
            rig.Vm.DailyCostLine);
        Assert.Equal("Total daily cost: CR3.45/day", rig.Vm.TotalLine);
        Assert.Equal((Car.Key, true), Assert.Single(rig.Toggles));

        rig.Vm.ToggleCommand.Execute(Row(rig, Car.Key));
        Assert.False(rig.Garage.Find(Car.Key)!.HasInsurance);
        Assert.Equal("Total daily cost: CR0.00/day", rig.Vm.TotalLine);
        Assert.Equal((Car.Key, false), rig.Toggles[1]);
    }

    [Fact]
    public void Collect_ShowsTheBillThePayoutAndTheCashAfter()
    {
        var rig = New(pending: 10.0);

        rig.Vm.OpenCollectCommand.Execute(null);

        Assert.True(rig.Vm.IsCollectView);
        Assert.True(rig.Vm.CanCollect);
        // Unity divides the escrow by the payout share to name the bill it came
        // from, then shows the transfer and the balance it lands on.
        Assert.Equal("Total repair cost: CR11.11", rig.Vm.CollectRepairLine);
        Assert.Equal("What the insurance will pay you: CR10.00", rig.Vm.CollectPayLine);
        Assert.Equal("Your cash after transfer: CR510.00", rig.Vm.CollectCashLine);
    }

    [Fact]
    public void CollectItVerb_OnlyShowsInsideTheEscrowBlock()
    {
        var rig = New(pending: 10.0);

        // COLLECT opens the block; the transfer verb lives inside it, so a full
        // escrow is not enough to put it on the landing page.
        Assert.True(rig.Vm.CanCollect);
        Assert.False(rig.Vm.ShowCollectIt);

        rig.Vm.OpenCollectCommand.Execute(null);
        Assert.True(rig.Vm.ShowCollectIt);

        rig.Vm.CollectItCommand.Execute(null);
        Assert.False(rig.Vm.ShowCollectIt);
    }

    [Fact]
    public void CollectIt_MovesTheEscrowToCash_AndResetsTheScreen()
    {
        var rig = New(pending: 10.0);
        rig.Vm.OpenCollectCommand.Execute(null);

        rig.Vm.CollectItCommand.Execute(null);

        Assert.Equal(510.0, rig.Wallet.Cash, 6);
        Assert.Equal(0.0, rig.Wallet.PendingInsurance);
        Assert.Equal(10.0, Assert.Single(rig.Collected));
        // Unity OnCollectItClicked: the verb goes away and the screen resets.
        Assert.False(rig.Vm.CanCollect);
        Assert.True(rig.Vm.IsLandingView);
    }

    [Fact]
    public void EmptyEscrow_HasNothingToCollect()
    {
        var rig = New();
        rig.Vm.OpenCollectCommand.Execute(null);

        Assert.False(rig.Vm.CanCollect);
        Assert.False(rig.Vm.ShowCollectIt);
        rig.Vm.CollectItCommand.Execute(null);

        Assert.Empty(rig.Collected);
        Assert.True(rig.Vm.IsCollectView);
    }

    [Fact]
    public void EmptyGarage_ShowsTheHint()
    {
        var rig = New(withCars: false);
        rig.Vm.ManageCommand.Execute(null);

        Assert.True(rig.Vm.IsEmpty);
        Assert.Empty(rig.Vm.Rows);
        Assert.Equal("No cars to insure yet.", rig.Vm.EmptyLine);
        Assert.Equal(string.Empty, rig.Vm.ValueCarLine);
        Assert.Equal("Total daily cost: CR0.00/day", rig.Vm.TotalLine);
    }

    [Fact]
    public void Back_ClosesTheOpenBlock_ThenLeavesTheShop()
    {
        bool navigated = false;
        var rig = New(onBack: () => navigated = true);
        rig.Vm.ManageCommand.Execute(null);

        rig.Vm.BackCommand.Execute(null);
        Assert.True(rig.Vm.IsLandingView);
        Assert.False(navigated);

        rig.Vm.BackCommand.Execute(null);
        Assert.True(navigated);
    }

    [Fact]
    public void Voice_SpeaksTheVerbsOfTheBlockThePlayerSees()
    {
        var rig = New(insured: true, pending: 10.0);

        // Landing: the two verbs and BACK.
        Assert.Equal(3, rig.Vm.VoiceActions.Count);
        Assert.Contains("MANAGE", rig.Vm.VoiceActions.Keys);
        Assert.Contains("COLLECT", rig.Vm.VoiceActions.Keys);
        Assert.Contains("BACK", rig.Vm.VoiceActions.Keys);

        rig.Vm.ManageCommand.Execute(null);
        // Cover list: both car names, the selected row's verb and BACK (the
        // other row's verb is not the one the page is offering).
        Assert.Contains(Row(rig, Car.Key).Title, rig.Vm.VoiceActions.Keys);
        Assert.Contains(Row(rig, Other.Key).Title, rig.Vm.VoiceActions.Keys);
        Assert.Contains("ADD", rig.Vm.VoiceActions.Keys);
        Assert.DoesNotContain("REMOVE", rig.Vm.VoiceActions.Keys);
        Assert.DoesNotContain("MANAGE", rig.Vm.VoiceActions.Keys);

        // The verb follows the selection: the covered car's row offers REMOVE.
        rig.Vm.SelectCommand.Execute(Row(rig, Car.Key));
        Assert.Contains("REMOVE", rig.Vm.VoiceActions.Keys);
        Assert.DoesNotContain("ADD", rig.Vm.VoiceActions.Keys);

        rig.Vm.OpenCollectCommand.Execute(null);
        Assert.Equal(2, rig.Vm.VoiceActions.Count);
        Assert.Contains("COLLECT IT", rig.Vm.VoiceActions.Keys);
        Assert.Contains("BACK", rig.Vm.VoiceActions.Keys);
    }

    [Fact]
    public void Voice_BackGlowsThenActs()
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
