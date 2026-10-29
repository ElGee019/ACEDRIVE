using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The Saturday run headless: the pickup leg names the dropoff list, the
/// pickup ENTER pins the first order, each corner ENTER settles exactly one
/// order through its receipt, CONTINUE moves to the next corner, and the last
/// CONTINUE hands the player back to the calendar.
/// </summary>
public sealed class TouristenfahrtenFlowTests
{
    [Fact]
    public void Begin_PinsThePickupWithTheDropoffList()
    {
        Harness run = Create();
        run.Flow.Begin();

        Assert.True(run.Flow.InProgress);
        Assert.Equal(1, run.Host.HidePanelCalls);
        HudJobGuidance guidance = Assert.IsType<HudJobGuidance>(run.Hud.JobGuidance);
        Assert.Equal("HudTouristenPickupDrive", guidance.DriveKey);
        Assert.Equal("HudTouristenPickupArrived", guidance.ArrivedKey);
        Assert.Equal("HudPickUp", guidance.ActionKey);
        Assert.Equal("Test Café|Brünnchen", string.Join('|', guidance.DriveArgs!));
    }

    [Fact]
    public void Deliver_AtTheShop_PinsTheFirstCornerWithItsOrder()
    {
        Harness run = Create();
        run.Flow.Begin();
        run.Flow.Deliver();

        HudJobGuidance guidance = Assert.IsType<HudJobGuidance>(run.Hud.JobGuidance);
        Assert.Equal("HudTouristenDropoffDrive", guidance.DriveKey);
        Assert.Equal("HudDeliveryStoreArrived", guidance.ArrivedKey);
        Assert.Equal("HudDeliveryStoreAction", guidance.ActionKey);
        Assert.Equal("2 × coffee|Brünnchen", string.Join('|', guidance.DriveArgs!));
        Assert.Equal(0, run.Settlement.SettleCalls);
    }

    [Fact]
    public void Deliver_AtTheCorner_PricesTheDrivenLegAndSettlesOnce()
    {
        Harness run = Create();
        run.Flow.Begin();
        Drive(run.Meter, 50f, 14.4); // 0.2 km to the shop: the player's own drive
        run.Flow.Deliver(); // load the goods: the corner legs start at zero
        Assert.Equal(0.0, run.Meter.DistanceKm);

        Drive(run.Meter, 50f, 36.0); // 0.5 km shop -> corner
        run.Flow.Deliver();

        Assert.Equal(1, run.Settlement.SettleCalls);
        Assert.Equal(0.5, run.Settlement.Deliveries[0].DistanceKm, 3);
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
        run.Flow.Begin();
        run.Flow.Deliver(); // load

        AddressLocation first = Assert.IsType<HudJobGuidance>(run.Hud.JobGuidance).Target;
        Drive(run.Meter, 50f, 36.0); // 0.5 km to the first corner
        run.Flow.Deliver();
        Assert.Equal(0.5, run.Settlement.Deliveries[0].DistanceKm, 3);

        run.Settlement.ContinueReceipt();

        Assert.Equal(2, run.Host.HidePanelCalls);
        Assert.Equal(0.0, run.Meter.DistanceKm); // the next leg starts at zero
        HudJobGuidance next = Assert.IsType<HudJobGuidance>(run.Hud.JobGuidance);
        Assert.NotEqual(first, next.Target);
        Assert.Equal("2 × coffee|" + next.TargetName, string.Join('|', next.DriveArgs!));

        Drive(run.Meter, 50f, 7.2); // 0.1 km
        run.Flow.Deliver();
        Assert.Equal(2, run.Settlement.SettleCalls);
        Assert.Equal(0.1, run.Settlement.Deliveries[1].DistanceKm, 3);
        run.Settlement.ContinueReceipt();

        Assert.Equal(1, run.Host.CalendarCalls);
        Assert.False(run.Flow.InProgress);
    }

    [Fact]
    public void Begin_WithoutAPickupDoor_DoesNothing()
    {
        Harness run = Create(hasPickup: false);
        run.Flow.Begin();

        Assert.False(run.Flow.InProgress);
        Assert.Equal(0, run.Host.HidePanelCalls);
        Assert.Null(run.Hud.JobGuidance);
    }

    [Fact]
    public void Begin_WhileARunIsLive_IsIgnored()
    {
        Harness run = Create();
        run.Flow.Begin();
        run.Flow.Begin();

        Assert.Equal(1, run.Doors.PickupCalls);
        Assert.Equal(1, run.Host.HidePanelCalls);
    }

    private static Harness Create(bool twoStops = false, bool hasPickup = true)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        TouristenMenu menu = Menu(twoStops);
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
        var flow = new TouristenfahrtenFlow(
            menu, doors, hud, settlement, meter, TaxiRules.Default,
            new Random(7), new TestLog());
        flow.Attach(host);
        return new Harness(flow, hud, host, settlement, doors, meter);
    }

    // One coffee item with a fixed quantity: the order text is then exact
    // ("2 × coffee"), so the assertion pins the populated sentence without
    // depending on a random draw.
    private static TouristenMenu Menu(bool twoStops) => TouristenMenu.Default with
    {
        Pickups =
        [
            new TouristenPickup(
                "coffee", PoiTypes.CoffeeShop, "coffee_beans", ["Test Café"],
                [new TouristenItemRule("coffee", "TouristenItemCoffee", 3.5, 2, 2)]),
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
        TaxiDriveMeter Meter);
}
