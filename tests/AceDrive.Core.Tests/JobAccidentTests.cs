using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The crash prompt (Unity's accident arrival): a crash that lands
/// during a job abandons the ride without a receipt and costs the customer one
/// friendship, a crash in free roam offers the repair as well, and the
/// companion's REPAIR THE CAR button pays the bill, banks the insured share for
/// the broker and hands a job's panel back to the main page. The first repair
/// of a career is also the moment the insurance broker opens: its door joins
/// the map and its explanation page interrupts the drive. None of it happens
/// through the introduction: the feed opens once the community centre is done.
/// </summary>
public sealed class JobAccidentTests
{
    private static readonly CarSpec Car = new(
        "ks_test_car", 2020, "Test", "Car", Passengers: 2, Class: 16,
        Price: 40000.0, TopSpeedKmh: 240);

    // A career past the guided tour: the community centre is the last leg the
    // introduction drives, and the crash feed only starts after it.
    private static OnboardingState TourDone =>
        OnboardingState.Fresh with { BirthdayDone = true, CommunityCentreDone = true };

    private static Garage GarageWith(bool insured) =>
        new([Car], new GarageState(Car.Key, [
            new OwnedCarState(Car.Key, 1, 0.0, HasInsurance: insured),
        ]));

    // The rig opens before the insurance premium's due hour (01:00), so these
    // suites read the crash money alone; the premium itself is WorldTicks' and
    // InsuranceFlow's to pin.
    private static FakeClock BeforeThePremium() =>
        new() { Now = new DateTime(2026, 10, 12, 0, 30, 0, DateTimeKind.Utc) };

    // A past-the-tour save whose login bonus is already banked for the rig's day:
    // the boot must not add the streak CR the cash assertions read.
    private static DriverState BonusBanked() =>
        new(0.0, 0.0, LastDailyBonusDate: "2026-10-12");

    private static TestSessionResult New(
        Garage garage, FakeTelemetryHud hud, FakeFriendshipStore friends,
        DriverWallet wallet, PoiDirectory? poiDirectory = null, Trunk? trunk = null) =>
        MainViewModelTests.Create(
            speedKmh: 0f, hud: hud, friendships: friends, wallet: wallet,
            cars: [Car], carKsId: Car.Key, garage: garage,
            poiDirectory: poiDirectory, clock: BeforeThePremium(),
            progress: TourDone, savedDriver: BonusBanked(), trunk: trunk);

    // One carried reward card in the compartment the coordinator reads.
    private static Trunk Carrying(DriverWallet wallet, RewardCardEffect effect)
    {
        var trunk = new Trunk();
        new Glovebox(RewardTable.Default, wallet).Claim(
            RewardTable.Default.Find(effect)!, trunk);
        return trunk;
    }

    // One crash: a poll at speed, then a poll whose damage counter jumped. The
    // detector prices the hit off the speed of the sample before it.
    private static void Crash(
        MainViewModel vm, FakeClock clock, FakeTelemetry tel,
        float damageBefore = 0f, float damageAfter = 50f)
    {
        tel.Next = tel.Next with { SpeedKmh = 120f, CarDamage = damageBefore };
        clock.Advance(TimeSpan.FromSeconds(0.1));
        vm.Poll();
        tel.Next = tel.Next with { SpeedKmh = 0f, CarDamage = damageAfter };
        clock.Advance(TimeSpan.FromSeconds(0.1));
        vm.Poll();
    }

    [Fact]
    public void AccidentDuringARide_EndsTheJobAndOffersTheRepair()
    {
        var hud = new FakeTelemetryHud();
        var friends = new FakeFriendshipStore();
        TestSessionResult rig = New(GarageWith(insured: false), hud, friends, new DriverWallet());
        TaxiFlow.Propose(rig.Vm);
        TaxiFlow.Pickup(rig.Vm, rig.Clock);

        Crash(rig.Vm, rig.Clock, rig.Tel);

        Assert.Null(hud.JobGuidance); // the drive line went away
        Assert.Equal("HudAccidentRideLost", hud.TaskPrompt!.TextKey);
        Assert.Equal("HudAccidentRepair", hud.TaskPrompt.ActionKey);
        // The prompt carries the impact speed, the customer who lost the
        // friendship and the bill the button takes (120 of 240 km/h on a
        // 40 000 CR car: a fortieth of its value).
        Assert.Equal("120", hud.TaskPrompt.Args![0]);
        var (customer, delta) = Assert.Single(friends.Deltas);
        Assert.Equal(customer, hud.TaskPrompt.Args[1]);
        Assert.Equal("1000.00", hud.TaskPrompt.Args[2]);
        Assert.Equal(-1, delta);
        // The ride cannot be resumed: both job commands guard on the phase, and
        // the crash dropped it to None.
        Assert.False(rig.Vm.DropoffCommand.CanExecute(null));
        Assert.False(rig.Vm.PickupCommand.CanExecute(null));
    }

    [Fact]
    public void RepairAfterAnInsuredAccident_TakesTheCash_ClaimsNinetyPercent_AndShowsTheMenu()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        Garage garage = GarageWith(insured: true);
        TestSessionResult rig = New(garage, hud, new FakeFriendshipStore(), wallet);
        TaxiFlow.Propose(rig.Vm);
        TaxiFlow.Pickup(rig.Vm, rig.Clock);
        Crash(rig.Vm, rig.Clock, rig.Tel); // 120 of 240 km/h: a fortieth of the car's value

        hud.RaiseTaskAction();

        Assert.Equal(49000.0, wallet.Cash, 6);              // 40000 × 0.025 paid
        Assert.Equal(900.0, wallet.PendingInsurance, 6);    // 90 % of the bill
        Assert.Equal(0.0, garage.Current!.BodyDamage, 6);
        Assert.Null(hud.TaskPrompt);
        Assert.IsType<MenuScreenViewModel>(rig.Vm.CurrentScreen);
        Assert.True(rig.Vm.IsShown); // the panel comes back on the main page
    }

    [Fact]
    public void RepairOnAnUninsuredCar_TakesTheWholeBillAndClaimsNothing()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        TestSessionResult rig = New(GarageWith(insured: false), hud, new FakeFriendshipStore(), wallet);
        TaxiFlow.Propose(rig.Vm);
        TaxiFlow.Pickup(rig.Vm, rig.Clock);
        Crash(rig.Vm, rig.Clock, rig.Tel);

        hud.RaiseTaskAction();

        Assert.Equal(49000.0, wallet.Cash, 6);
        Assert.Equal(0.0, wallet.PendingInsurance, 6);
        Assert.IsType<MenuScreenViewModel>(rig.Vm.CurrentScreen);
    }

    [Fact]
    public void RepairWithShortCash_LeavesTheDebtAndClaimsTheWholeBill()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(500.0, 0.0));
        Garage garage = GarageWith(insured: true);
        TestSessionResult rig = New(garage, hud, new FakeFriendshipStore(), wallet);
        TaxiFlow.Propose(rig.Vm);
        TaxiFlow.Pickup(rig.Vm, rig.Clock);
        Crash(rig.Vm, rig.Clock, rig.Tel);

        hud.RaiseTaskAction();

        // The repair is not optional: the 1 000 CR bill leaves a 500 CR debt
        // (Unity's RepairScreen subtracts regardless) and the claim is 90 % of
        // the bill, not of what the cash happened to hold.
        Assert.Equal(-500.0, wallet.Cash, 6);
        Assert.Equal(900.0, wallet.PendingInsurance, 6);
        Assert.Equal(0.0, garage.Current!.BodyDamage, 6);
    }

    [Fact]
    public void AccidentOutsideAJob_AlsoOffersTheRepair()
    {
        var hud = new FakeTelemetryHud();
        var friends = new FakeFriendshipStore();
        TestSessionResult rig = New(GarageWith(insured: false), hud, friends, new DriverWallet());

        Crash(rig.Vm, rig.Clock, rig.Tel);

        // No job was live, so the prompt names no lost friendship: just the
        // impact speed and the bill the button takes (120 of 240 km/h on a
        // 40 000 CR car: a fortieth of its value).
        Assert.NotNull(hud.TaskPrompt);
        Assert.Equal("HudAccidentSolo", hud.TaskPrompt!.TextKey);
        Assert.Equal("HudAccidentRepair", hud.TaskPrompt.ActionKey);
        Assert.Equal("120", hud.TaskPrompt.Args![0]);
        Assert.Equal("1000.00", hud.TaskPrompt.Args[1]);
        Assert.Equal(2, hud.TaskPrompt.Args.Count);
        Assert.Empty(friends.Deltas);
    }

    [Fact]
    public void RepairOutsideAJob_TakesTheCash_ClaimsTheInsuredShare_AndStaysHidden()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        Garage garage = GarageWith(insured: true);
        TestSessionResult rig = New(garage, hud, new FakeFriendshipStore(), wallet);
        // The free-roam drive keeps the panel down; only a call or a job raises it.
        if (rig.Vm.IsShown)
        {
            rig.Vm.ToggleShowCommand.Execute(null);
        }
        Assert.False(rig.Vm.IsShown);

        Crash(rig.Vm, rig.Clock, rig.Tel);
        hud.RaiseTaskAction();

        Assert.Equal(49000.0, wallet.Cash, 6);              // 40000 × 0.025 paid
        Assert.Equal(900.0, wallet.PendingInsurance, 6);    // 90 % of the bill
        Assert.Equal(0.0, garage.Current!.BodyDamage, 6);
        Assert.Null(hud.TaskPrompt);
        // A free-roam crash is not an interruption: the drive keeps the screen.
        Assert.False(rig.Vm.IsShown);
    }

    [Fact]
    public void TheFirstCrashRepair_OpensTheInsuranceBroker_AndShowsItsPage()
    {
        // A career that never met a broker: the crash repair is the moment the
        // first broker door joins the map, and the page that explains insurance
        // opens on top of the drive with the bill the player just paid.
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        var directory = new PoiDirectory();
        TestSessionResult rig = New(
            GarageWith(insured: false), hud, new FakeFriendshipStore(), wallet,
            directory);
        if (rig.Vm.IsShown)
        {
            rig.Vm.ToggleShowCommand.Execute(null);
        }

        Crash(rig.Vm, rig.Clock, rig.Tel);
        hud.RaiseTaskAction();

        Assert.Equal(49000.0, wallet.Cash, 6);
        PointOfInterest broker = Assert.Single(
            directory.All, poi => poi.Type == PoiTypes.Insurance);
        var intro = Assert.IsType<PoiIntroScreenViewModel>(rig.Vm.CurrentScreen);
        Assert.Equal(Strings.Get("PoiIntroInsuranceTitle"), intro.Title);
        // This rig drives the test car, not Grandma Sonja's, so the page keeps
        // the plain body: only her car earned the closing line.
        Assert.DoesNotContain(
            Strings.Get("PoiIntroInsuranceGrandma"), intro.Body, StringComparison.Ordinal);
        Assert.True(rig.Vm.IsShown);

        // CONTINUE hands the drive back: the page was the interruption, not a
        // new place for the player to be left in.
        intro.ContinueCommand.Execute(null);
        Assert.False(rig.Vm.IsShown);

        // The moment is once: the next crash pays its bill and opens nothing,
        // and the door the first one earned stays the only broker.
        rig.Clock.Advance(TimeSpan.FromSeconds(2));
        Crash(rig.Vm, rig.Clock, rig.Tel, damageBefore: 50f, damageAfter: 100f);
        hud.RaiseTaskAction();

        Assert.False(rig.Vm.IsShown);
        Assert.Equal(
            broker.Id,
            Assert.Single(directory.All, poi => poi.Type == PoiTypes.Insurance).Id);
    }

    [Fact]
    public void FirstRepairOnAFullZone_StillOpensTheBrokerPage()
    {
        // A career whose whole discovered area is built over (one street, every
        // house taken — what the old negative-cash save reset left behind) must
        // still get the broker and its page: the door falls back to the next
        // street of the map instead of silently dropping the moment.
        AddressMap map = AddressMap.Default;
        AddressStreet zoneStreet = map.Streets[0];
        var directory = new PoiDirectory();
        for (int number = 1; number <= HouseNumberPlan.Count(zoneStreet, map); number++)
        {
            directory.Add(new PointOfInterest(
                $"story_{number}", $"Story Door {number}",
                new AddressLocation(zoneStreet.Name, number, HouseNumberPlan.SideOf(number))));
        }
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        TestSessionResult rig = MainViewModelTests.Create(
            speedKmh: 0f, hud: hud, wallet: wallet, cars: [Car], carKsId: Car.Key,
            garage: GarageWith(insured: false), poiDirectory: directory,
            addressMap: map, progress: TourDone,
            savedDriver: BonusBanked() with { StreetIds = [OperatingZone.HomeStreetId] });

        Crash(rig.Vm, rig.Clock, rig.Tel);
        hud.RaiseTaskAction();

        PointOfInterest broker = Assert.Single(
            directory.All, poi => poi.Type == PoiTypes.Insurance);
        Assert.Equal(map.Streets[1].Name, broker.Address.StreetName);
        Assert.IsType<PoiIntroScreenViewModel>(rig.Vm.CurrentScreen);
        Assert.True(rig.Vm.IsShown);
    }

    [Fact]
    public void GrandmasCar_IsCovered_SoTheFirstCrashPaysTheClaim()
    {
        // Grandma Sonja hands her car over covered (the walk's pick and the
        // boot's seed both set it), and the broker's page says so: the crash
        // repair then recovers Unity's 90 % into the escrow the broker collects
        // instead of billing the player the whole bill.
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        var directory = new PoiDirectory();
        TestSessionResult rig = MainViewModelTests.Create(
            speedKmh: 0f, hud: hud, wallet: wallet, cars: [Car], carKsId: Car.Key,
            garage: GarageWith(insured: true), poiDirectory: directory,
            progress: TourDone with { GrandmaCarId = Car.Key },
            clock: BeforeThePremium(), savedDriver: BonusBanked());

        Crash(rig.Vm, rig.Clock, rig.Tel);
        hud.RaiseTaskAction();

        Assert.Equal(49000.0, wallet.Cash, 6);           // the bill is still taken
        Assert.Equal(900.0, wallet.PendingInsurance, 6); // 90 % of the 1 000 CR bill
        var intro = Assert.IsType<PoiIntroScreenViewModel>(rig.Vm.CurrentScreen);
        Assert.EndsWith(
            Strings.Get("PoiIntroInsuranceGrandma"), intro.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void InvincibleRidesCard_SkipsTheCrashEntirely()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        Trunk trunk = Carrying(wallet, RewardCardEffect.InvincibleRides);
        Garage garage = GarageWith(insured: false);
        TestSessionResult rig = New(garage, hud, new FakeFriendshipStore(), wallet, trunk: trunk);
        TaxiFlow.Propose(rig.Vm);
        TaxiFlow.Pickup(rig.Vm, rig.Clock);

        Crash(rig.Vm, rig.Clock, rig.Tel);

        // No accident: no dent, no prompt, and the ride is still running. The
        // card keeps its rides until the settle ticks the completed job.
        Assert.Null(hud.TaskPrompt);
        Assert.Equal(0.0, garage.Current!.BodyDamage, 6);
        Assert.True(rig.Vm.DropoffCommand.CanExecute(null));
        Assert.Contains(rig.Trunk.Items, item => item.Name == "INVINCIBLE RIDES");
    }

    [Fact]
    public void DrProtectionCard_KeepsTheRatingAndIsSpent()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        Trunk trunk = Carrying(wallet, RewardCardEffect.DriverRatingProtection);
        TestSessionResult rig = New(
            GarageWith(insured: false), hud, new FakeFriendshipStore(), wallet, trunk: trunk);
        TaxiFlow.Propose(rig.Vm);
        TaxiFlow.Pickup(rig.Vm, rig.Clock);

        Crash(rig.Vm, rig.Clock, rig.Tel);

        Assert.Equal(DriverRating.NewGame, wallet.Rating);
        Assert.Contains(
            hud.TaskPrompt!.Notes!, note => note.TextKey == "RewardDrProtectionNote");
        Assert.DoesNotContain(rig.Trunk.Items, item => item.Name == "DR PROTECTION");
    }

    [Fact]
    public void APlainCrash_DropsTheDrivingSkillsByTheBand()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        TestSessionResult rig = New(
            GarageWith(insured: false), hud, new FakeFriendshipStore(), wallet);
        TaxiFlow.Propose(rig.Vm);
        TaxiFlow.Pickup(rig.Vm, rig.Clock);

        Crash(rig.Vm, rig.Clock, rig.Tel);

        // A fresh 20-overall driver is in the lowest band: every skill loses 1.
        Assert.Equal(19.0, wallet.Rating.Cornering);
        Assert.Equal(19.0, wallet.Rating.Concentration);
        Assert.Equal(19.0, wallet.Rating.OffTheLine);
    }

    [Fact]
    public void AccidentLoyaltyShield_KeepsTheCustomerFriendship()
    {
        var hud = new FakeTelemetryHud();
        var friends = new FakeFriendshipStore();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        Trunk trunk = Carrying(wallet, RewardCardEffect.AccidentLoyalty);
        TestSessionResult rig = New(
            GarageWith(insured: false), hud, friends, wallet, trunk: trunk);
        TaxiFlow.Propose(rig.Vm);
        TaxiFlow.Pickup(rig.Vm, rig.Clock);

        Crash(rig.Vm, rig.Clock, rig.Tel);

        Assert.Empty(friends.Deltas);
        Assert.Equal("HudAccidentRideLostShield", hud.TaskPrompt!.TextKey);
        Assert.Contains(
            hud.TaskPrompt.Notes!, note => note.TextKey == "RewardAccidentLoyaltyCustomerNote");
        Assert.DoesNotContain(rig.Trunk.Items, item => item.Name == "ACCIDENT LOYALTY SHIELD");
    }

    [Fact]
    public void HalfDamageCard_HalvesTheQuotedBill_AndIsSpentByTheRepair()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        Trunk trunk = Carrying(wallet, RewardCardEffect.HalfDamage);
        TestSessionResult rig = New(
            GarageWith(insured: false), hud, new FakeFriendshipStore(), wallet, trunk: trunk);
        TaxiFlow.Propose(rig.Vm);
        TaxiFlow.Pickup(rig.Vm, rig.Clock);

        Crash(rig.Vm, rig.Clock, rig.Tel);

        // 120 of 240 km/h on a 40 000 CR car is a 1 000 CR bill; half is 500.
        Assert.Equal("500.00", hud.TaskPrompt!.Args![2]);
        Assert.Contains(
            hud.TaskPrompt.Notes!, note => note.TextKey == "RewardHalfDamageNote");

        hud.RaiseTaskAction();

        Assert.Equal(49500.0, wallet.Cash, 6);
        Assert.Equal(0.0, wallet.PendingInsurance, 6);
        Assert.DoesNotContain(rig.Trunk.Items, item => item.Name == "HALF DAMAGE REDUCTION");
    }

    [Fact]
    public void FreeCarRepairCard_QuotesZeroAndChargesNothing()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        Trunk trunk = Carrying(wallet, RewardCardEffect.FreeCarRepair);
        Garage garage = GarageWith(insured: true);
        TestSessionResult rig = New(
            garage, hud, new FakeFriendshipStore(), wallet, trunk: trunk);
        TaxiFlow.Propose(rig.Vm);
        TaxiFlow.Pickup(rig.Vm, rig.Clock);

        Crash(rig.Vm, rig.Clock, rig.Tel);

        Assert.Equal("0.00", hud.TaskPrompt!.Args![2]);
        Assert.Contains(
            hud.TaskPrompt.Notes!, note => note.TextKey == "RewardFreeRepairNote");

        hud.RaiseTaskAction();

        Assert.Equal(50000.0, wallet.Cash, 6);
        // A free repair paid no bill, so there is no insured share to claim.
        Assert.Equal(0.0, wallet.PendingInsurance, 6);
        Assert.Equal(0.0, garage.Current!.BodyDamage, 6);
        Assert.DoesNotContain(rig.Trunk.Items, item => item.Name == "FREE CAR REPAIR");
    }

    [Fact]
    public void CrashingThroughTheIntroduction_IsNotDetected()
    {
        // The first drives are grandma's errand and the guided tour (the
        // community centre is its last leg): a bump there bills nothing, offers
        // no repair and opens no broker page. The dent counts from the moment
        // the centre hands the career over.
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        var directory = new PoiDirectory();
        Garage garage = GarageWith(insured: false);
        TestSessionResult rig = MainViewModelTests.Create(
            speedKmh: 0f, hud: hud, wallet: wallet, cars: [Car], carKsId: Car.Key,
            garage: garage, poiDirectory: directory, clock: BeforeThePremium());
        Assert.True(rig.Story.IntroInProgress);

        Crash(rig.Vm, rig.Clock, rig.Tel);

        Assert.Null(hud.TaskPrompt);
        Assert.Equal(0, hud.ShowTaskTextCalls); // no companion summon either
        Assert.Equal(0.0, garage.Current!.BodyDamage, 6);
        Assert.Equal(50000.0, wallet.Cash, 6);
        Assert.Empty(directory.All);
    }

    [Fact]
    public void FreeRoamCrashOverAnOpenPanel_SurfacesTheCompanion_ThenTheRepairShowsTheBrokerOnThePanel()
    {
        // The panel is up on a page, so the companion has stepped aside: the
        // crash must still reach the player, which is the host surfacing the
        // companion's task text over the page. Taking the offer then hands the
        // panel the broker page the first crash earned, and CONTINUE gives the
        // interrupted page back.
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        var directory = new PoiDirectory();
        TestSessionResult rig = New(
            GarageWith(insured: false), hud, new FakeFriendshipStore(), wallet,
            directory);
        rig.Story.FinishDialogue();
        rig.Vm.Show();
        Assert.True(rig.Vm.IsShown);
        object? page = rig.Vm.CurrentScreen;

        Crash(rig.Vm, rig.Clock, rig.Tel);

        Assert.Equal(1, hud.ShowTaskTextCalls);
        Assert.Equal("HudAccidentSolo", hud.TaskPrompt!.TextKey);

        hud.RaiseTaskAction();

        var intro = Assert.IsType<PoiIntroScreenViewModel>(rig.Vm.CurrentScreen);
        Assert.Equal(Strings.Get("PoiIntroInsuranceTitle"), intro.Title);
        Assert.True(rig.Vm.IsShown);

        intro.ContinueCommand.Execute(null);

        // The page the crash interrupted comes back, still on the open panel.
        Assert.Same(page, rig.Vm.CurrentScreen);
        Assert.True(rig.Vm.IsShown);
    }

    [Fact]
    public void FreeRoamCrashOverAnOpenWalletPage_RepaintsTheCashTheRepairTook()
    {
        // The interrupted page is handed back as the same instance, so its
        // figures have to be re-read first: the repair behind the broker page
        // paid the bill, and the CR line the player left cannot come back with
        // the pre-repair saldo.
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        var directory = new PoiDirectory();
        TestSessionResult rig = New(
            GarageWith(insured: false), hud, new FakeFriendshipStore(), wallet,
            directory);
        rig.Story.FinishDialogue();
        rig.Vm.Show();
        var menu = Assert.IsType<MenuScreenViewModel>(rig.Vm.CurrentScreen);
        menu.WalletCommand.Execute(null);
        var page = Assert.IsType<WalletScreenViewModel>(rig.Vm.CurrentScreen);
        Assert.Equal("CR50000.00", page.CashAmount);

        Crash(rig.Vm, rig.Clock, rig.Tel); // 120 of 240 km/h on a 40 000 CR car
        hud.RaiseTaskAction();
        var intro = Assert.IsType<PoiIntroScreenViewModel>(rig.Vm.CurrentScreen);

        intro.ContinueCommand.Execute(null);

        Assert.Same(page, rig.Vm.CurrentScreen);
        Assert.Equal("CR49000.00", page.CashAmount);
    }
}
