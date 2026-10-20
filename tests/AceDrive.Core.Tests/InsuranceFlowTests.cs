using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The insurance money loop (PORTPLAN item 34): the nightly per-car
/// premium fires once per game date, an insured repair pays the escrow, and a
/// toggle banks through the host.</summary>
public sealed class InsuranceFlowTests
{
    private static readonly CarSpec PricedCar = new(
        "ks_test_car", 2020, "Test", "Car", Passengers: 2, Class: 16, Price: 12000.0, EngineCc: 2000);

    private static IReadOnlyList<CarSpec> Pool => [PricedCar];

    private static Garage GarageWith(bool insured) =>
        new([PricedCar], new GarageState(PricedCar.Key, [
            new OwnedCarState(PricedCar.Key, 1, 0.0, insured),
        ]));

    [Fact]
    public void Poll_ChargesTheDailyPremiumOncePerGameDate()
    {
        var garage = GarageWith(insured: true);
        var wallet = new DriverWallet(new DriverState(100.0, 0.0));
        var (vm, clock, _, _) = MainViewModelTests.Create(
            speedKmh: 0f, garage: garage, wallet: wallet, cars: Pool);

        // 13.5 game hours (2.2 real seconds each): noon → 01:30 the next day,
        // safely inside the rules' 01:00 charge hour.
        clock.Advance(TimeSpan.FromSeconds(13.5 * 60 * 2.2));
        vm.Poll();

        Assert.Equal(100.0 - 3.45, wallet.Cash, 6); // 6.90 base − 50% driver rating
        Assert.NotNull(wallet.LastInsuranceChargeDate);

        vm.Poll(); // same date and hour: no second charge
        Assert.Equal(100.0 - 3.45, wallet.Cash, 6);
    }

    [Fact]
    public void Poll_SkipsUninsuredCars()
    {
        var garage = GarageWith(insured: false);
        var wallet = new DriverWallet(new DriverState(100.0, 0.0));
        var (vm, clock, _, _) = MainViewModelTests.Create(
            speedKmh: 0f, garage: garage, wallet: wallet, cars: Pool);

        clock.Advance(TimeSpan.FromSeconds(13.5 * 60 * 2.2));
        vm.Poll();

        Assert.Equal(100.0, wallet.Cash, 6);
    }

    [Fact]
    public void MechanicRepair_OnAnInsuredCar_PaysTheEscrow()
    {
        var garage = GarageWith(insured: true);
        garage.Current!.Drive(1200.0); // fully wears the oil part
        var wallet = new DriverWallet(new DriverState(1000.0, 0.0));
        var (vm, _, _, _) = MainViewModelTests.Create(
            speedKmh: 0f, garage: garage, wallet: wallet, cars: Pool);

        vm.MechanicCommand.Execute(null);
        var mechanic = Assert.IsType<MechanicScreenViewModel>(vm.CurrentScreen);
        mechanic.AddCommand.Execute(mechanic.Rows.Single(r => r.Key == "oil_change"));
        mechanic.RepairPartsCommand.Execute(null);

        Assert.Equal(1000.0 - 250.0, wallet.Cash, 6);
        Assert.Equal(250.0 * 0.9, wallet.PendingInsurance, 6);
    }

    [Fact]
    public void MechanicRepair_OnAnUninsuredCar_PaysNothing()
    {
        var garage = GarageWith(insured: false);
        garage.Current!.Drive(1200.0);
        var wallet = new DriverWallet(new DriverState(1000.0, 0.0));
        var (vm, _, _, _) = MainViewModelTests.Create(
            speedKmh: 0f, garage: garage, wallet: wallet, cars: Pool);

        vm.MechanicCommand.Execute(null);
        var mechanic = Assert.IsType<MechanicScreenViewModel>(vm.CurrentScreen);
        mechanic.AddCommand.Execute(mechanic.Rows.Single(r => r.Key == "oil_change"));
        mechanic.RepairPartsCommand.Execute(null);

        Assert.Equal(1000.0 - 250.0, wallet.Cash, 6);
        Assert.Equal(0.0, wallet.PendingInsurance);
    }

    [Fact]
    public void InsuranceToggle_BanksThroughTheHost()
    {
        var garage = GarageWith(insured: false);
        var store = new FakeGarageStore();
        var (vm, _, _, _) = MainViewModelTests.Create(
            speedKmh: 0f, garage: garage, garageStore: store, cars: Pool);

        vm.InsuranceCommand.Execute(null);
        var insurance = Assert.IsType<InsuranceScreenViewModel>(vm.CurrentScreen);
        insurance.ToggleCommand.Execute(insurance.Rows.Single(r => r.Key == PricedCar.Key));

        Assert.True(garage.Current!.HasInsurance);
        Assert.Equal(1, store.Saves);
        Assert.True(store.Load()!.Cars!.Single().HasInsurance);
    }
}
