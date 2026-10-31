using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The Saturday run headless: the calendar tap opens the briefing and draws
/// nothing, the briefing's START draws the shop and 1-3 corners and splits one
/// job order across them, the pickup leg names the dropoff list, the pickup
/// ENTER pins the first corner's share, each corner ENTER settles exactly one
/// order through its receipt, CONTINUE moves to the next corner, and the last
/// CONTINUE hands the player back to the calendar.
/// </summary>
public sealed class TouristenfahrtenFlowTests
{
    [Fact]
    public void Begin_OpensTheBriefingWithoutDrawingTheRun()
    {
        Harness run = Create();

        run.Flow.Begin();

        var page = Assert.IsType<TouristenBriefingScreenViewModel>(
            Assert.Single(run.Host.Briefings));
        Assert.False(run.Flow.InProgress);
        Assert.Equal(0, run.Doors.PickupCalls);
        Assert.Equal(0, run.Host.HidePanelCalls);
        Assert.Null(run.Hud.JobGuidance);
        // An ordinary day wears the run's own weekend title.
        Assert.Equal("TOURISTENFAHRTEN WEEKEND", page.Title);
        Assert.Equal("START DRIVE", page.StartLabel);
    }

    [Fact]
    public void Begin_OnARaceWeekend_TitlesTheBriefingWithTheEvent()
    {
        // The real-world calendar names the event: the page reads as the NLS
        // round, not as the generic weekend.
        Harness run = Create(weekends: RaceWeekend("NLS Round 1"));

        run.Flow.Begin();

        var page = Assert.IsType<TouristenBriefingScreenViewModel>(
            Assert.Single(run.Host.Briefings));
        Assert.Equal("NLS Round 1", page.Title);
    }

    [Fact]
    public void Deliver_OnARaceWeekend_PricesTheGoodsWithTheBonus()
    {
        Harness run = Create(weekends: RaceWeekend("NLS Round 1"));
        StartBriefing(run);
        run.Flow.Deliver(); // load the goods
        run.Flow.Deliver(); // settle the corner

        Assert.Equal(
            2.0, Assert.Single(run.Settlement.Deliveries).PayoutMultiplier);
    }

    [Fact]
    public void BriefingStart_DrawsAndPinsThePickupWithTheDropoffList()
    {
        Harness run = Create();
        StartBriefing(run);

        Assert.True(run.Flow.InProgress);
        Assert.Equal(1, run.Host.HidePanelCalls);
        HudJobGuidance guidance = Assert.IsType<HudJobGuidance>(run.Hud.JobGuidance);
        Assert.Equal("HudTouristenPickupDrive", guidance.DriveKey);
        Assert.Equal("HudTouristenPickupArrived", guidance.ArrivedKey);
        Assert.Equal("HudPickUp", guidance.ActionKey);
        // The leg's own values follow the shared address shape the HUD adds.
        Assert.Equal("Brünnchen", Assert.Single(guidance.DriveArgs!));
    }

    [Fact]
    public void BriefingBack_ReturnsToTheCalendarWithoutDrawingTheRun()
    {
        Harness run = Create();
        run.Flow.Begin();
        var page = Assert.IsType<TouristenBriefingScreenViewModel>(
            Assert.Single(run.Host.Briefings));

        page.BackCommand.Execute(null);

        Assert.Equal(1, run.Host.CalendarCalls);
        Assert.False(run.Flow.InProgress);
        Assert.Equal(0, run.Doors.PickupCalls);
        Assert.Equal(0, run.Host.HidePanelCalls);
        Assert.Null(run.Hud.JobGuidance);
    }

    [Fact]
    public void Briefing_AnswersByVoice_GlowThenStart()
    {
        Harness run = Create();
        run.Flow.Begin();
        var page = Assert.IsType<TouristenBriefingScreenViewModel>(
            Assert.Single(run.Host.Briefings));

        page.VoiceActions["START DRIVE"]();

        // The glow ack owns the first beat; the run is drawn only after it.
        Assert.True(page.StartFlash);
        Assert.False(run.Flow.InProgress);
        run.Clock.Advance(VoiceBeat.Duration);
        run.Beat.Poll();

        Assert.True(run.Flow.InProgress);
        page.VoiceActions[Strings.Get("TouristenBack")]();
        Assert.True(page.BackFlash);
    }

    [Fact]
    public void Deliver_AtTheShop_PinsTheFirstCornerWithItsOrder()
    {
        Harness run = Create();
        StartBriefing(run);
        run.Flow.Deliver();

        HudJobGuidance guidance = Assert.IsType<HudJobGuidance>(run.Hud.JobGuidance);
        Assert.Equal("HudTouristenDropoffDrive", guidance.DriveKey);
        Assert.Equal("HudTouristenDropoffArrived", guidance.ArrivedKey);
        Assert.Equal("HudTouristenDeliverAction", guidance.ActionKey);
        // The corner's own copy: the button prints DELIVER, not the delivery
        // career's freight wording, and the arrival line names the same word.
        Assert.Equal("DELIVER", Strings.Get(guidance.ActionKey));
        Assert.Contains(
            "DELIVER", Strings.Format(guidance.ArrivedKey, guidance.TargetName));
        Assert.Equal("2 × coffee", Assert.Single(guidance.DriveArgs!));
        Assert.Equal(0, run.Settlement.SettleCalls);
    }

    [Fact]
    public void Deliver_AtTheCorner_PricesTheDrivenLegAndSettlesOnce()
    {
        Harness run = Create();
        StartBriefing(run);
        Drive(run.Meter, 50f, 14.4); // 0.2 km to the shop: the player's own drive
        run.Flow.Deliver(); // load the goods: the corner legs start at zero
        Assert.Equal(0.0, run.Meter.DistanceKm);

        Drive(run.Meter, 50f, 36.0); // 0.5 km shop -> corner
        run.Flow.Deliver();

        Assert.Equal(1, run.Settlement.SettleCalls);
        Assert.Equal(0.5, run.Settlement.Deliveries[0].DistanceKm, 3);
        // An ordinary day pays the order's own price: no calendar bonus.
        Assert.Equal(1.0, run.Settlement.Deliveries[0].PayoutMultiplier);
        Assert.True(run.Flow.InProgress);

        // The receipt is up: a second ENTER may not settle the order twice.
        run.Flow.Deliver();
        Assert.Equal(1, run.Settlement.SettleCalls);

        run.Settlement.ContinueReceipt();

        Assert.False(run.Flow.InProgress);
        Assert.Equal(1, run.Host.CalendarCalls);
        Assert.Null(run.Hud.JobGuidance);
    }

    [Fact]
    public void Continue_PinsTheNextCornerAndHidesThePanelAgain()
    {
        Harness run = Create(twoStops: true);
        StartBriefing(run);
        run.Flow.Deliver(); // load

        // The menu caps the whole job order at two units, and the split's
        // one-per-stop pass leaves one at each corner: a per-dropoff draw would
        // have loaded two at every stop instead of dividing the shop's order.
        Assert.Equal(
            "1 × coffee", Assert.Single(Assert.IsType<HudJobGuidance>(run.Hud.JobGuidance).DriveArgs!));
        AddressLocation first = Assert.IsType<HudJobGuidance>(run.Hud.JobGuidance).Target;
        Drive(run.Meter, 50f, 36.0); // 0.5 km to the first corner
        run.Flow.Deliver();
        Assert.Equal(0.5, run.Settlement.Deliveries[0].DistanceKm, 3);

        run.Settlement.ContinueReceipt();

        Assert.Equal(2, run.Host.HidePanelCalls);
        Assert.Equal(0.0, run.Meter.DistanceKm); // the next leg starts at zero
        HudJobGuidance next = Assert.IsType<HudJobGuidance>(run.Hud.JobGuidance);
        Assert.NotEqual(first, next.Target);
        Assert.Equal("1 × coffee", Assert.Single(next.DriveArgs!));
        Assert.Equal("HudTouristenDeliverAction", next.ActionKey);

        Drive(run.Meter, 50f, 7.2); // 0.1 km
        run.Flow.Deliver();
        Assert.Equal(2, run.Settlement.SettleCalls);
        Assert.Equal(0.1, run.Settlement.Deliveries[1].DistanceKm, 3);
        run.Settlement.ContinueReceipt();

        Assert.Equal(1, run.Host.CalendarCalls);
        Assert.False(run.Flow.InProgress);
    }

    [Fact]
    public void Begin_SkipsADropoffThatGetsNoShareOfTheOrder()
    {
        // One unit, two drawn corners: the second share is empty, so the run
        // never opens that door or tells the player to deliver nothing there.
        Harness run = Create(twoStops: true, maxItems: 1);
        StartBriefing(run);

        Assert.True(run.Flow.InProgress);
        Assert.Equal(1, run.Doors.DropoffCalls);
        HudJobGuidance guidance = Assert.IsType<HudJobGuidance>(run.Hud.JobGuidance);
        Assert.Equal("HudTouristenPickupDrive", guidance.DriveKey);
        Assert.Equal("Brünnchen", Assert.Single(guidance.DriveArgs!));

        run.Flow.Deliver(); // load
        Assert.Equal("1 × coffee", Assert.Single(
            Assert.IsType<HudJobGuidance>(run.Hud.JobGuidance).DriveArgs!));
        run.Flow.Deliver();
        run.Settlement.ContinueReceipt();

        Assert.Equal(1, run.Host.CalendarCalls);
    }

    [Fact]
    public void StartRun_WithoutAPickupDoor_DoesNothing()
    {
        Harness run = Create(hasPickup: false);
        StartBriefing(run);

        Assert.False(run.Flow.InProgress);
        Assert.Equal(0, run.Host.HidePanelCalls);
        Assert.Null(run.Hud.JobGuidance);
    }

    [Fact]
    public void StartRun_WhileARunIsLive_IsIgnored()
    {
        Harness run = Create();
        StartBriefing(run);
        run.Flow.Begin();
        run.Flow.StartRun();

        // A live run owns the HUD: the row opens no second briefing over it and
        // START draws nothing new.
        Assert.Single(run.Host.Briefings);
        Assert.Equal(1, run.Doors.PickupCalls);
        Assert.Equal(1, run.Host.HidePanelCalls);
    }

    private static Harness Create(
        bool twoStops = false, bool hasPickup = true, int maxItems = 2,
        TouristenRaceCalendar? weekends = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        TouristenMenu menu = Menu(twoStops, maxItems);
        var doors = new FakeTouristenDoors();
        if (hasPickup)
        {
            doors.PickupDoor = new PointOfInterest(
                "test-cafe", "Test Café",
                new AddressLocation("Street 1", 1, HouseSide.Left),
                PoiTypes.CoffeeShop, "Test", "coffee_beans");
        }

        int number = 2;
        foreach (TouristenDropoffDefinition place in menu.Dropoffs)
        {
            doors.Doors[place.Id] = new PointOfInterest(
                place.Id, place.Name,
                new AddressLocation("Street 1", number, HouseSide.Left),
                PoiTypes.TouristenDropoff);
            number++;
        }

        var hud = new FakeTelemetryHud();
        var settlement = new FakeTouristenSettlement();
        var host = new FakeTouristenHost();
        var meter = new TaxiDriveMeter(TaxiRules.Default.MaxMeterSpeedKmh);
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var flow = new TouristenfahrtenFlow(
            menu, doors, hud, settlement, meter, TaxiRules.Default,
            beat, clock, weekends ?? TouristenRaceCalendar.None, new Random(7), new TestLog());
        flow.Attach(host);
        return new Harness(flow, hud, host, settlement, doors, meter, clock, beat);
    }

    // A one-day event on the fake clock's own date (2026-10-12), so a test's
    // run is on the calendar without moving time first.
    private static TouristenRaceCalendar RaceWeekend(string name) => new(
        [new TouristenRaceWeekend(
            new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 12), name)],
        2.0);

    // The calendar tap and the briefing's click on START: the two input steps
    // every leg test needs, so the run itself is what those tests assert.
    private static void StartBriefing(Harness run)
    {
        run.Flow.Begin();
        Assert.IsType<TouristenBriefingScreenViewModel>(Assert.Single(run.Host.Briefings))
            .StartCommand.Execute(null);
    }

    // One coffee item with a fixed quantity: the order text is then exact
    // ("2 × coffee"), so the assertion pins the populated sentence without
    // depending on a random draw. The menu's floor stays two per dropoff; a
    // low maxItems caps the whole job order below it, which is what the split
    // and the skipped-stop tests lean on.
    private static TouristenMenu Menu(bool twoStops, int maxItems) => TouristenMenu.Default with
    {
        Pickups =
        [
            new TouristenPickup(
                "coffee", PoiTypes.CoffeeShop, "coffee_beans", ["Test Café"],
                [new TouristenItemRule("coffee", "TouristenItemCoffee", 3.5,
                    Math.Min(2, maxItems), maxItems)]),
        ],
        Dropoffs = twoStops
            ?
            [
                new TouristenDropoffDefinition("corner-a", "Brünnchen"),
                new TouristenDropoffDefinition("corner-b", "Pflanzgarten"),
            ]
            : [new TouristenDropoffDefinition("corner-a", "Brünnchen")],
        MinDropoffs = twoStops ? 2 : 1,
        MaxDropoffs = twoStops ? 2 : 1,
        MinItemsPerOrder = 2,
    };

    // The meter only accepts poll-sized ticks (0..1 s), so a test leg is a
    // run of 0.1 s samples like the 10 Hz loop would feed it.
    private static void Drive(TaxiDriveMeter meter, float speedKmh, double seconds)
    {
        while (seconds > 0.0)
        {
            double dt = Math.Min(0.1, seconds);
            meter.Add(speedKmh, dt);
            seconds -= dt;
        }
    }

    private sealed record Harness(
        TouristenfahrtenFlow Flow, FakeTelemetryHud Hud, FakeTouristenHost Host,
        FakeTouristenSettlement Settlement, FakeTouristenDoors Doors,
        TaxiDriveMeter Meter, FakeClock Clock, VoiceBeat Beat);
}
