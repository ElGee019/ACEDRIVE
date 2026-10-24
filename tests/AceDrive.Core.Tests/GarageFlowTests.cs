using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Garage + dealer flows through the ViewModel (PORTPLAN items 29/30):
/// the starter pick becomes the first owned car, the garage page banks a switch,
/// and a purchase runs cash -&gt; garage -&gt; the drive-new / keep-current choice.</summary>
public sealed class GarageFlowTests
{
    private static readonly CarSpec Mx5 = new(
        "ks_mazda_mx5_na", 1990, "Mazda", "MX-5 NA", Passengers: 1, Class: 9,
        Price: 12000.0, TrunkLitres: 150);

    private static readonly CarSpec Junior = new(
        "ks_alfa_romeo_junior", 2024, "Alfa Romeo", "junior", Passengers: 4, Class: 6,
        Price: 47500.0, TrunkLitres: 400);

    private static readonly IReadOnlyList<CarSpec> Pool = [Mx5, Junior];

    private static OnboardingContent Content() => new(
        ["Mazda"],
        new Dictionary<string, string[]>(),
        [new GrandmaCar(Mx5.Id, "Mazda MX-5 NA", 1990, 12000, null),
         new GrandmaCar(Junior.Id, "Alfa Romeo Junior", 2024, 47500, null)],
        [], [], [], []);

    [Fact]
    public void Boot_SeedsGrandmasCarIntoAnEmptyGarage()
    {
        // Every save written before this slice has a finished onboarding and no
        // garage: the starter it picked is seeded instead of opening empty.
        var garage = new Garage(Pool);
        var store = new FakeGarageStore();
        _ = MainViewModelTests.Create(
            cars: Pool, garage: garage, garageStore: store, content: Content(),
            progress: OnboardingState.Fresh with { GrandmaCarId = Mx5.Key, BirthdayDone = true });

        Assert.True(garage.Owns(Mx5.Key));
        Assert.Equal(Mx5.Key, garage.CurrentKey);
        Assert.Equal(1, store.Saves);
        Assert.Equal(Mx5.Key, store.Load()!.CurrentKey);
    }

    [Fact]
    public void Boot_LeavesAResumedGarageAlone()
    {
        var garage = new Garage(Pool, new GarageState(
            Junior.Key, [new OwnedCarState(Junior.Key, 2, 12.0, true)]));
        var store = new FakeGarageStore();
        _ = MainViewModelTests.Create(
            cars: Pool, garage: garage, garageStore: store, content: Content(),
            progress: OnboardingState.Fresh with { GrandmaCarId = Mx5.Key, BirthdayDone = true });

        OwnedCar car = Assert.Single(garage.Cars);
        Assert.Equal(Junior.Key, car.Key);
        Assert.Equal(12.0, car.Odometer);
        Assert.True(car.HasInsurance);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void Boot_ResumesTheAskQuizHistoryAndSnapshotsItBack()
    {
        // The car-stat quiz history is per car (PORTPLAN item 11): a resumed
        // garage must hand it to the quiz and write it back on the next save.
        var garage = new Garage(Pool, new GarageState(
            Junior.Key,
            [new OwnedCarState(Junior.Key, AskedCategories: ["weight", "power"])]));

        OwnedCar car = Assert.Single(garage.Cars);
        Assert.Equal(["weight", "power"], car.AskedCategories);

        car.MarkAskedCategory("topSpeed", categoryCount: 6);
        Assert.Equal(["weight", "power", "topSpeed"],
            Assert.Single(garage.Snapshot().Cars!).AskedCategories!);
    }

    [Fact]
    public void ChooseCar_MakesTheStarterTheFirstOwnedCar()
    {
        var garage = new Garage(Pool);
        var store = new FakeGarageStore();
        var session = MainViewModelTests.Create(
            cars: Pool, garage: garage, garageStore: store, content: Content());

        session.Story.ChooseBrand("Mazda");
        session.Story.ChooseCar(Mx5.Key);

        Assert.True(garage.Owns(Mx5.Key));
        Assert.Equal(Mx5.Key, garage.CurrentKey);
        Assert.Equal(1, store.Saves);

        // Walking the introduction again keeps the car that already exists.
        session.Story.ChooseCar(Mx5.Key);
        Assert.Single(garage.Cars);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public void ChooseCar_SwitchesCurrentWhenTheGarageAlreadyHasACar()
    {
        // The intro can be walked again with a resumed garage. Grandma's pick
        // must still become the car driven from here on, not just join the
        // garage beside the old current car.
        var garage = new Garage(Pool, new GarageState(
            Junior.Key, [new OwnedCarState(Junior.Key, 1, 0.0, false)]));
        var store = new FakeGarageStore();
        var session = MainViewModelTests.Create(
            cars: Pool, garage: garage, garageStore: store, content: Content());

        session.Story.ChooseBrand("Mazda");
        session.Story.ChooseCar(Mx5.Key);

        Assert.Equal(Mx5.Key, garage.CurrentKey);
        Assert.Equal(2, garage.Cars.Count);
        Assert.Equal(1, store.Saves);
        Assert.Equal(Mx5.Key, store.Load()!.CurrentKey);
    }

    [Fact]
    public void GaragePage_SwitchBanksTheChoice()
    {
        var garage = new Garage(Pool, new GarageState(
            Junior.Key, [new OwnedCarState(Junior.Key, 1, 0.0, false),
                         new OwnedCarState(Mx5.Key, 2, 0.0, false)]));
        var store = new FakeGarageStore();
        var (vm, _, _, _) = MainViewModelTests.Create(
            cars: Pool, garage: garage, garageStore: store, content: Content());
        vm.ToggleShowCommand.Execute(null); // hide
        vm.ToggleShowCommand.Execute(null); // call it back: the menu page
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);

        // Unity's You menu owns the garage: the HOME tile, then GARAGE.
        menu.HomeCommand.Execute(null);
        var home = Assert.IsType<HomeScreenViewModel>(vm.CurrentScreen);
        home.GarageCommand.Execute(null);
        var page = Assert.IsType<GarageScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(2, page.Cars.Count);

        page.SelectedCar = Assert.Single(page.Cars, r => r.Key == Mx5.Key);
        page.SwitchCommand.Execute(null);
        Assert.Equal(Mx5.Key, garage.CurrentKey);
        Assert.Equal(1, store.Saves);
        Assert.Equal(Mx5.Key, store.Load()!.CurrentKey);
        // The page stays open on the switch; BACK is the way out, to the home
        // page it was opened from (Unity's You menu).
        Assert.Same(page, vm.CurrentScreen);
        page.BackCommand.Execute(null);
        Assert.IsType<HomeScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void DealerBuy_DriveNew_SwitchesTheCarAndBanksBothSteps()
    {
        var garage = new Garage(Pool, new GarageState(
            Junior.Key, [new OwnedCarState(Junior.Key, 1, 0.0, false)]));
        var store = new FakeGarageStore();
        var wallet = new DriverWallet(new DriverState(15000.0, 0.0));
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = MainViewModelTests.Create(
            cars: Pool, garage: garage, garageStore: store, wallet: wallet, content: Content(),
            progress: OnboardingState.Fresh with { BirthdayDone = true }, hud: hud,
            savedDriver: new DriverState(0.0, 0.0, LastDailyBonusDate: "2026-10-12"));

        var dealer = MainViewModelTests.OpenPoi<DealerScreenViewModel>(vm, hud, "Car Dealer");
        Assert.DoesNotContain(Junior.Key, dealer.Rows.Select(r => r.Key));

        dealer.SelectCommand.Execute(Assert.Single(dealer.Rows, r => r.Key == Mx5.Key));
        dealer.BuyCommand.Execute(null);

        Assert.True(garage.Owns(Mx5.Key));
        Assert.Equal(3000.0, wallet.Cash);
        Assert.Equal(Junior.Key, garage.CurrentKey);
        Assert.Equal(1, store.Saves);

        var choice = Assert.IsType<BoughtFirstCarScreenViewModel>(vm.CurrentScreen);
        Assert.True(choice.HasSummary); // the garage's second car
        choice.DriveCommand.Execute(null);

        Assert.Equal(Mx5.Key, garage.CurrentKey);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(2, store.Saves);
        Assert.Equal(Mx5.Key, store.Load()!.CurrentKey);
    }

    [Fact]
    public void DealerBuy_KeepCurrent_LeavesTheDrivenCarAlone()
    {
        var garage = new Garage(Pool, new GarageState(
            Junior.Key, [new OwnedCarState(Junior.Key, 1, 0.0, false)]));
        var store = new FakeGarageStore();
        var wallet = new DriverWallet(new DriverState(15000.0, 0.0));
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = MainViewModelTests.Create(
            cars: Pool, garage: garage, garageStore: store, wallet: wallet, content: Content(),
            progress: OnboardingState.Fresh with { BirthdayDone = true }, hud: hud,
            savedDriver: new DriverState(0.0, 0.0, LastDailyBonusDate: "2026-10-12"));

        var dealer = MainViewModelTests.OpenPoi<DealerScreenViewModel>(vm, hud, "Car Dealer");
        dealer.SelectCommand.Execute(Assert.Single(dealer.Rows, r => r.Key == Mx5.Key));
        dealer.BuyCommand.Execute(null);

        var choice = Assert.IsType<BoughtFirstCarScreenViewModel>(vm.CurrentScreen);
        choice.KeepCommand.Execute(null);

        Assert.True(garage.Owns(Mx5.Key));
        Assert.Equal(Junior.Key, garage.CurrentKey);
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.Equal(Junior.Key, store.Load()!.CurrentKey);
    }

    [Fact]
    public void DealerBuy_WithoutTheCash_BuysNothing()
    {
        var garage = new Garage(Pool);
        var store = new FakeGarageStore();
        var wallet = new DriverWallet(new DriverState(100.0, 0.0));
        var hud = new FakeTelemetryHud();
        var (vm, _, _, _) = MainViewModelTests.Create(
            cars: Pool, garage: garage, garageStore: store, wallet: wallet, content: Content(),
            progress: OnboardingState.Fresh with { BirthdayDone = true }, hud: hud,
            savedDriver: new DriverState(0.0, 0.0, LastDailyBonusDate: "2026-10-12"));

        var dealer = MainViewModelTests.OpenPoi<DealerScreenViewModel>(vm, hud, "Car Dealer");
        dealer.SelectCommand.Execute(Assert.Single(dealer.Rows, r => r.Key == Mx5.Key));
        dealer.BuyCommand.Execute(null);

        Assert.Empty(garage.Cars);
        Assert.Equal(100.0, wallet.Cash);
        Assert.Equal(0, store.Saves);
        Assert.Same(dealer, vm.CurrentScreen);
    }

    [Fact]
    public void JobsPage_HasNoGarageDoor_HomePageDoes()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var garage = new Garage(Pool);
        var (vm, _, _, _) = MainViewModelTests.Create(
            cars: Pool, garage: garage, content: Content(),
            progress: OnboardingState.Fresh with { GrandmaCarId = Mx5.Key, BirthdayDone = true });
        vm.ToggleShowCommand.Execute(null); // hide
        vm.ToggleShowCommand.Execute(null); // call it back: the menu page
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        // The Jobs tile opens the control page carrying every job door — the
        // garage is a You-menu page in Unity, so it is not one of them.
        menu.JobsCommand.Execute(null);
        var page = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);

        Assert.DoesNotContain("GARAGE", page.VoiceActions.Keys);
        Assert.DoesNotContain("dealer", page.VoiceActions.Keys);

        // The home page owns the garage and addresses doors, by click and by voice.
        page.BackCommand.Execute(null);
        menu.HomeCommand.Execute(null);
        var home = Assert.IsType<HomeScreenViewModel>(vm.CurrentScreen);
        Assert.Contains("GARAGE", home.VoiceActions.Keys);
        Assert.Contains("ADDRESSES", home.VoiceActions.Keys);
        home.GarageCommand.Execute(null);
        Assert.IsType<GarageScreenViewModel>(vm.CurrentScreen);
    }
}
