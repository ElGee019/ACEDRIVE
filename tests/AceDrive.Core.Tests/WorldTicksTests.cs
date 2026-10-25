using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The game-clock world on its own (PORTPLAN items 25/26/34): the town's
/// citizens and shelves, the appetite and the nightly premium. The coordinator
/// is not involved, so each test sees only this collaborator's decisions: which
/// boundary paid, which transition was logged and which save it ran.
/// </summary>
public sealed class WorldTicksTests
{
    // The FakeClock opens on Monday 12:00; a game hour is 60 x the game clock's
    // 2.2 real seconds per minute.
    private static readonly DateTime MondayNoon = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan OneGameMinute =
        TimeSpan.FromSeconds(GameClock.RealSecondsPerGameMinute);
    private static readonly TimeSpan OneGameHour = OneGameMinute * 60;

    // A full shelf gains one citizen per 30 game minutes, like the shipped
    // CityRules.Default (same store the coordinator's town test uses).
    private static readonly DeliveryStore FullMarket =
        new("full-market", "Full Market", "Schmitz", 40, 40, 4.40);

    // A priced car so the nightly premium has an amount; the test pool rows are
    // "not for sale" (Price 0) and would never be charged.
    private static readonly CarSpec Insured =
        new("ks_test_insured", 2020, "Test", "Insured", Passengers: 4, Class: 5, Price: 20000.0);

    private sealed record Fixture(
        WorldTicks World, FakeClock Clock, CityEconomy City, Hunger Hunger,
        Garage Garage, DriverWallet Wallet, FakeDriverStore Drivers,
        FakeHungerStore HungerStore, TestLog Log);

    public WorldTicksTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    private static Fixture Build(DateTime gameStart, Hunger? hunger = null, CarSpec? car = null)
    {
        var clock = new FakeClock { Now = gameStart };
        var game = new GameClock(clock, gameStart);
        var log = new TestLog();
        // One catalogue behind the town and the board: the board log line sums
        // the cells of the stores the board lists, exactly like production.
        var catalog = new DeliveryCatalog([FullMarket], 0.7, 100);
        var city = new CityEconomy(catalog.Stores, CityRules.Default);
        var appetite = hunger ?? new Hunger(HungerRules.Default);
        var wallet = new DriverWallet();
        IReadOnlyList<CarSpec> pool = car is null ? [] : [car];
        var garage = new Garage(pool);
        if (car is not null)
        {
            garage.Add(car.Key, week: 0).SetInsurance(insured: true);
        }
        var driverStore = new FakeDriverStore();
        var hungerStore = new FakeHungerStore();
        var rivals = new FakeRivals();
        var career = new DeliveryCareer(HubCatalog.Default, catalog.StartCredP);
        var board = new DeliveryBoard(catalog);
        // One instance per service graph: the fleet charges the wallet the
        // assertions read, and the saver banks into the driver store they read.
        var fleet = new FleetService(
            garage, pool, game, wallet, InsuranceRules.Default, log,
            new FakeCarBrandSource(), new FakeContentSource(TestSession.EmptyContent));
        var saver = new GameStateSaver(
            clock, game, new FakeGameClockStore(), driverStore, new FakeGarageStore(),
            new FakeCityStore(), hungerStore, new FakeRoadTestStore(),
            new FakeShiftStore(), wallet, new DailyStreak(clock, null),
            new OperatingZone(null, seed: 0), new Trunk(), garage, city, appetite,
            new RoadTestLedger(pool, seed: 0), new TaxiShift(), new ShiftRecord(null),
            new JobLedger(), new DestinationPins(), new VisitLedger());
        var world = new WorldTicks(
            game, city, appetite, fleet, board, career, saver, rivals, log);
        return new Fixture(world, clock, city, appetite, garage, wallet, driverStore, hungerStore, log);
    }

    [Fact]
    public void Tick_BeforeTheTownIsShown_AnchorsWithoutPaying()
    {
        // A resumed session owes nothing for the hours the process was closed:
        // the town starts living at the first game moment it is shown.
        var fixture = Build(MondayNoon);
        fixture.Clock.Advance(TimeSpan.FromDays(7));

        Assert.False(fixture.World.Tick(onboardingDone: true));
        Assert.Equal(100, fixture.City.TotalCitizens([FullMarket]));
    }

    [Fact]
    public void Tick_TownGrowsOnlyOnWholeGameMinutes()
    {
        var fixture = Build(MondayNoon);
        _ = fixture.World.Tick(onboardingDone: true); // primes the town

        fixture.Clock.Advance(TimeSpan.FromSeconds(60)); // 27 game minutes: too early
        Assert.False(fixture.World.Tick(true));
        Assert.Equal(100, fixture.City.TotalCitizens([FullMarket]));

        fixture.Clock.Advance(OneGameMinute * 4); // 31 game minutes: one tick
        Assert.True(fixture.World.Tick(true));
        Assert.Equal(101, fixture.City.TotalCitizens([FullMarket]));
        Assert.Contains(
            fixture.Log.Infos,
            line => line.StartsWith("city: 101 citizens", StringComparison.Ordinal));
    }

    [Fact]
    public void Tick_HungerPaysAnHourAtATime_OnlyPastTheIntroduction()
    {
        // Unity's firstSceneComplete gate: the appetite stays frozen through the
        // introduction, and the frozen hours never pay out afterwards.
        var fixture = Build(MondayNoon);
        _ = fixture.World.Tick(onboardingDone: false); // anchors the frozen meter

        fixture.Clock.Advance(OneGameHour * 2);
        _ = fixture.World.Tick(false);
        Assert.Equal(0, fixture.Hunger.Value);

        fixture.Clock.Advance(OneGameHour);
        _ = fixture.World.Tick(true);
        Assert.Equal(1, fixture.Hunger.Value);
        Assert.Contains("[Hunger] 1/36", fixture.Log.Infos);
    }

    [Fact]
    public void Tick_HungerGain_BanksTheReadingWithTheHour()
    {
        // A killed overlay never reaches the exit-only bank, so the meter banks
        // itself when the reading moves: at most a partial hour is lost, never
        // the session's appetite (one write per game hour, not per poll).
        var fixture = Build(MondayNoon);
        _ = fixture.World.Tick(onboardingDone: true); // anchors the meter
        Assert.Empty(fixture.HungerStore.Saved);

        // One game hour plus a minute: the hour boundary is the bank, and the
        // extra minute keeps the assertion off the clock's floating-point edge.
        fixture.Clock.Advance(OneGameHour + OneGameMinute);
        _ = fixture.World.Tick(true);

        HungerState saved = Assert.Single(fixture.HungerStore.Saved);
        Assert.Equal(1, saved.Hunger);
        Assert.False(saved.ThresholdTriggered);

        // Polls inside the same hour bank nothing.
        fixture.Clock.Advance(OneGameMinute);
        _ = fixture.World.Tick(true);
        Assert.Single(fixture.HungerStore.Saved);
    }

    [Fact]
    public void Tick_HungerAtTheBlockEdge_WarnsOnce()
    {
        // The warning fires on the transition into the blocked band, not per
        // poll and not per hour (Unity's TOO HUNGRY edge).
        var fixture = Build(MondayNoon, hunger: new Hunger(
            HungerRules.Default, new HungerState(34, ThresholdTriggered: false)));
        _ = fixture.World.Tick(onboardingDone: true); // anchors at 34

        fixture.Clock.Advance(OneGameHour);
        _ = fixture.World.Tick(true); // 35: blocked
        fixture.Clock.Advance(OneGameHour);
        _ = fixture.World.Tick(true); // still blocked: no second line

        Assert.Equal(35, fixture.Hunger.Value);
        Assert.True(fixture.Hunger.Blocks);
        Assert.Single(fixture.Log.Warnings, line =>
            line.StartsWith("[Hunger] job gate reached", StringComparison.Ordinal));
    }

    [Fact]
    public void Tick_ChargesTheNightlyPremiumOncePerGameDate_AndBanksIt()
    {
        // Insurance lands in the charge hour, once per game date, and the
        // charged premium is progress a kill would lose, so the shared driver
        // save runs with it. 01:00 is the shipped charge hour.
        var fixture = Build(
            new DateTime(2026, 10, 12, 1, 0, 0, DateTimeKind.Utc), car: Insured);
        double before = fixture.Wallet.Cash;

        _ = fixture.World.Tick(onboardingDone: true);
        Assert.True(fixture.Wallet.Cash < before);
        Assert.Single(fixture.Drivers.Saved);

        double charged = fixture.Wallet.Cash;
        _ = fixture.World.Tick(true);
        Assert.Equal(charged, fixture.Wallet.Cash);
        Assert.Single(fixture.Drivers.Saved);

        // The same charge hour one game day later charges again (a minute past
        // the hour keeps the assertion off the floating-point day edge).
        fixture.Clock.Advance(OneGameHour * 24 + OneGameMinute);
        _ = fixture.World.Tick(true);
        Assert.True(fixture.Wallet.Cash < charged);
        Assert.Equal(2, fixture.Drivers.Saved.Count);
    }

    [Fact]
    public void Tick_OutsideTheChargeHour_ChargesNothing()
    {
        // 12:00 is not the nightly charge hour: no premium, and no save per
        // poll either.
        var fixture = Build(MondayNoon, car: Insured);
        double before = fixture.Wallet.Cash;

        _ = fixture.World.Tick(onboardingDone: true);

        Assert.Equal(before, fixture.Wallet.Cash);
        Assert.Empty(fixture.Drivers.Saved);
    }
}
