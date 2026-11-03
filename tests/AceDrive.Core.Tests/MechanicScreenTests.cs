using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The car mechanic page (PORTPLAN items 31/32, port of Unity
/// CarMechanicScreen): part rows with efficiency and class-scaled prices, the
/// add-only repair queue, the engine repair/replace pair and the voice flow.</summary>
public sealed class MechanicScreenTests
{
    private static readonly CarSpec Car = new(
        "ks_test_car", 2020, "Test", "Car", Passengers: 2, Class: 16, Price: 40000.0, EngineCc: 2000);

    private sealed record Rig(
        MechanicScreenViewModel Vm, Garage Garage, DriverWallet Wallet, FakeClock Clock,
        VoiceBeat Beat, List<MechanicRepair> Repairs, Trunk Trunk);

    private static Rig New(
        double cash = 100000.0, double driveKm = 0.0, bool broken = false,
        CarSpec? car = null, bool withCar = true, Action? onBack = null,
        double bodyDamage = 0.0, bool freeRepair = false, bool halfDamage = false)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        CarSpec spec = car ?? Car;
        // With a car: a saved row so the class/price band and the broken flag
        // are exercised; without one: an empty garage.
        GarageState? saved = withCar
            ? new GarageState(spec.Key, [
                new OwnedCarState(spec.Key, 1, 0.0, false, 0, null, new EngineConditionState(0.0, broken),
                    BodyDamage: bodyDamage),
            ])
            : null;
        var garage = new Garage([spec], saved);
        if (driveKm > 0.0 && garage.Current is { } owned)
        {
            owned.Drive(driveKm);
        }
        var wallet = new DriverWallet(new DriverState(cash, 0.0));
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var repairs = new List<MechanicRepair>();
        var glovebox = new Glovebox(RewardTable.Default, wallet);
        var trunk = new Trunk();
        if (freeRepair)
        {
            glovebox.Claim(
                RewardTable.Default.Find(RewardCardEffect.FreeCarRepair)!, trunk);
        }
        if (halfDamage)
        {
            glovebox.Claim(
                RewardTable.Default.Find(RewardCardEffect.HalfDamage)!, trunk);
        }
        var vm = new MechanicScreenViewModel(
            garage, wallet, glovebox, trunk,
            new GameClock(clock, clock.UtcNow), beat,
            new RelayCommand(() => onBack?.Invoke()), repairs.Add);
        return new Rig(vm, garage, wallet, clock, beat, repairs, trunk);
    }

    private static MechanicPartRow Row(Rig rig, string key) => rig.Vm.Rows.Single(r => r.Key == key);

    [Fact]
    public void Rows_ShowEfficiencyAndTheClassScaledPrice()
    {
        var rig = New(driveKm: 1200.0); // class 16 → ×1.0

        MechanicPartRow oil = Row(rig, "oil_change"); // 1200/1200 → fully worn
        Assert.Equal("Oil efficiency", oil.Name);
        Assert.Equal("0.00%", oil.StatusText);
        Assert.Equal("CR250.00", oil.PriceText);

        MechanicPartRow tires = Row(rig, "tires"); // 1200/2500 = 48% worn
        Assert.Equal("52.00%", tires.StatusText);
        Assert.Equal("CR900.00", tires.PriceText);
    }

    [Fact]
    public void PartPrice_ScalesByClass()
    {
        var low = new CarSpec("ks_low", 2000, "Test", "Low", Passengers: 1, Class: 5, Price: 8000.0, EngineCc: 1400);
        var rig = New(driveKm: 1200.0, car: low);

        Assert.Equal("CR100.00", Row(rig, "oil_change").PriceText); // 250 × 0.4
        Assert.Equal("CR360.00", Row(rig, "tires").PriceText);      // 900 × 0.4
        Assert.Contains("CR320.00", rig.Vm.RepairEngineCostLine);   // 8000 × 0.10 × 0.4
    }

    [Fact]
    public void FreshParts_ReadServiced_AndCannotBeAdded()
    {
        var rig = New(); // nothing driven: every part at 0% wear

        MechanicPartRow oil = Row(rig, "oil_change");
        Assert.Equal("100.00%", oil.StatusText);
        Assert.Equal("✓", oil.ActionLabel);
        Assert.False(oil.AddEnabled);
    }

    [Fact]
    public void Add_QueuesAPart_AndHidesItsButton()
    {
        var rig = New(driveKm: 1200.0);
        rig.Vm.AddCommand.Execute(Row(rig, "oil_change"));

        MechanicPartRow oil = Row(rig, "oil_change");
        Assert.True(oil.Queued);
        Assert.False(oil.AddVisible);
        Assert.True(rig.Vm.RepairPartsVisible);
        Assert.True(rig.Vm.TotalVisible);
        Assert.Contains("CR250.00", rig.Vm.TotalLine);
    }

    [Fact]
    public void UnaffordablePart_CannotBeQueued()
    {
        var rig = New(cash: 100.0, driveKm: 1200.0);

        MechanicPartRow oil = Row(rig, "oil_change"); // CR250 > cash
        Assert.Equal("NO $", oil.ActionLabel);
        Assert.False(oil.AddEnabled);

        rig.Vm.AddCommand.Execute(oil);
        Assert.False(Row(rig, "oil_change").Queued);
        Assert.False(rig.Vm.RepairPartsVisible);
    }

    [Fact]
    public void RepairParts_SpendsTheTotal_AndServicesOnlyTheQueue()
    {
        var rig = New(cash: 1000.0, driveKm: 1200.0);
        rig.Vm.AddCommand.Execute(Row(rig, "oil_change")); // 250
        rig.Vm.AddCommand.Execute(Row(rig, "wipers"));     // 90

        rig.Vm.RepairPartsCommand.Execute(null);

        Assert.Equal(1000.0 - 340.0, rig.Wallet.Cash, 6);
        OwnedCar car = rig.Garage.Current!;
        Assert.Equal(0.0, car.Condition.PartFraction("oil_change", 1200.0));
        Assert.Equal(0.0, car.Condition.PartFraction("wipers", 1200.0));
        Assert.Equal(1200.0 / 2700.0, car.Condition.PartFraction("brake_pads", 1200.0), 6); // untouched
        MechanicRepair repair = Assert.Single(rig.Repairs);
        Assert.Equal("parts", repair.Kind);
        Assert.Equal(340.0, repair.Cost, 6);
        Assert.Equal(2, repair.PartsReplaced);
        Assert.False(rig.Vm.RepairPartsVisible); // the queue cleared
    }

    [Fact]
    public void EngineButtons_AppearOnlyWhenStranded()
    {
        Assert.False(New().Vm.EngineButtonsVisible);
        Assert.True(New(broken: true).Vm.EngineButtonsVisible);
    }

    [Fact]
    public void RepairEngine_SpendsAndClearsTheBreakdown_KeepingTheOdometer()
    {
        var rig = New(cash: 100000.0, driveKm: 5000.0, broken: true);

        rig.Vm.RepairEngineCommand.Execute(null);

        OwnedCar car = rig.Garage.Current!;
        Assert.False(car.Engine.Broken);
        Assert.Equal(5000.0, car.Engine.BaseOdometer);
        Assert.Equal(5000.0, car.Odometer);
        Assert.Equal(100000.0 - 4000.0, rig.Wallet.Cash, 6); // 40000 × 0.10 × 1.0
        Assert.Equal("engine-repair", Assert.Single(rig.Repairs).Kind);
    }

    [Fact]
    public void NewEngine_SpendsResetsTheOdometerAndTheParts()
    {
        var rig = New(cash: 100000.0, driveKm: 5000.0, broken: true);

        rig.Vm.NewEngineCommand.Execute(null);

        OwnedCar car = rig.Garage.Current!;
        Assert.False(car.Engine.Broken);
        Assert.Equal(0.0, car.Odometer);
        Assert.Equal(0.0, car.Engine.BaseOdometer);
        Assert.Equal(0.0, car.Condition.PartFraction("oil_change", 0.0));
        Assert.Equal(100000.0 - 12000.0, rig.Wallet.Cash, 6); // 40000 × 0.30
        Assert.True(Assert.Single(rig.Repairs).EngineReplaced);
    }

    [Fact]
    public void BodyDamage_ShowsTheBill_AndTheRepairClearsIt()
    {
        var rig = New(cash: 100000.0, bodyDamage: 0.5);

        Assert.True(rig.Vm.BodyRepairVisible);
        Assert.Contains("50.0%", rig.Vm.BodyDamageLine);
        Assert.Contains("CR20000.00", rig.Vm.BodyDamageLine); // 40000 × 0.5

        rig.Vm.RepairBodyCommand.Execute(null);

        Assert.Equal(0.0, rig.Garage.Current!.BodyDamage, 6);
        Assert.Equal(80000.0, rig.Wallet.Cash, 6);
        Assert.False(rig.Vm.BodyRepairVisible);
        MechanicRepair repair = Assert.Single(rig.Repairs);
        Assert.Equal("body-repair", repair.Kind);
        Assert.Equal(20000.0, repair.Cost, 6);
    }

    [Fact]
    public void AWrittenOffCar_BillsNoMoreThanItsOwnPrice()
    {
        var rig = New(cash: 100000.0, bodyDamage: 1.0);

        Assert.Contains("100.0%", rig.Vm.BodyDamageLine);
        Assert.Contains("CR40000.00", rig.Vm.BodyDamageLine); // the car's price, not more

        rig.Vm.RepairBodyCommand.Execute(null);

        Assert.Equal(60000.0, rig.Wallet.Cash, 6);
        Assert.Equal(40000.0, Assert.Single(rig.Repairs).Cost, 6);
    }

    [Fact]
    public void FreeCarRepairCard_CoversTheWholeBodyBill_AndIsSpent()
    {
        var rig = New(cash: 1000.0, bodyDamage: 0.5, freeRepair: true);

        // The card is offered on the body line and the button works even though
        // the cash could not cover the 20 000 CR bill.
        Assert.Contains("free repair", rig.Vm.BodyDamageLine, StringComparison.OrdinalIgnoreCase);
        Assert.True(rig.Vm.RepairBodyCommand.CanExecute(null));

        rig.Vm.RepairBodyCommand.Execute(null);

        Assert.Equal(0.0, rig.Garage.Current!.BodyDamage, 6);
        Assert.Equal(1000.0, rig.Wallet.Cash, 6); // nothing was charged
        Assert.DoesNotContain(
            rig.Trunk.Items, item => item.Name == "FREE CAR REPAIR");
        Assert.Equal(0.0, Assert.Single(rig.Repairs).Cost, 6);
    }

    [Fact]
    public void HalfDamageCard_HalvesTheBodyBill_AndIsSpent()
    {
        var rig = New(cash: 100000.0, bodyDamage: 0.5, halfDamage: true);

        Assert.Contains("Half Damage Reduction", rig.Vm.BodyDamageLine, StringComparison.Ordinal);
        Assert.Contains("CR10000.00", rig.Vm.BodyDamageLine, StringComparison.Ordinal);

        rig.Vm.RepairBodyCommand.Execute(null);

        Assert.Equal(0.0, rig.Garage.Current!.BodyDamage, 6);
        Assert.Equal(90000.0, rig.Wallet.Cash, 6); // half of the 20 000 CR bill
        Assert.DoesNotContain(rig.Trunk.Items, item => item.Name == "HALF DAMAGE REDUCTION");
        Assert.Equal(10000.0, Assert.Single(rig.Repairs).Cost, 6);
    }

    [Fact]
    public void ACleanCar_HidesTheBodyRepair()
    {
        var rig = New();

        Assert.False(rig.Vm.BodyRepairVisible);
        Assert.Equal(string.Empty, rig.Vm.BodyDamageLine);
        Assert.False(rig.Vm.RepairBodyCommand.CanExecute(null));
    }

    [Fact]
    public void BodyRepair_WithoutCash_IsANoOp()
    {
        var rig = New(cash: 100.0, bodyDamage: 0.5);

        rig.Vm.RepairBodyCommand.Execute(null);

        Assert.Equal(0.5, rig.Garage.Current!.BodyDamage, 6);
        Assert.Empty(rig.Repairs);
    }

    [Fact]
    public void Repair_WithoutCash_IsANoOp()
    {
        var rig = New(cash: 0.0, driveKm: 1200.0, broken: true);

        rig.Vm.RepairEngineCommand.Execute(null);
        rig.Vm.NewEngineCommand.Execute(null);

        Assert.True(rig.Garage.Current!.Engine.Broken);
        Assert.Equal(0.0, rig.Wallet.Cash);
        Assert.Empty(rig.Repairs);
    }

    [Fact]
    public void EmptyGarage_ShowsTheNoCarLine()
    {
        var rig = New(withCar: false);

        Assert.False(rig.Vm.HasCar);
        Assert.Equal("No car to service yet.", rig.Vm.CarLine);
        Assert.Empty(rig.Vm.Rows);
        Assert.False(rig.Vm.RepairPartsVisible);
    }

    [Fact]
    public void Voice_BackGlowsThenNavigates()
    {
        bool navigated = false;
        var rig = New(onBack: () => navigated = true);

        rig.Vm.VoiceActions["BACK"].Invoke();
        Assert.True(rig.Vm.BackFlash);
        Assert.False(navigated); // the glow shows before the page flips

        rig.Clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        rig.Beat.Poll();
        Assert.True(navigated);
        Assert.False(rig.Vm.BackFlash);
    }
}
