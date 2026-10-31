using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The haul's phase machine and its game-minute stamps: pickup only at
/// the job's dealer, delivery timed once, and the settle's minutes guard.</summary>
public sealed class CarHaulRunTests
{
    private static readonly CarHaulOffer Offer = new(
        new CarSpec("car-1", 1960, "Abarth", "Hatch", Class: 6),
        new PointOfInterest(
            "dealer-1", "Dealer", new AddressLocation("Street 1", 1, HouseSide.Right),
            PoiTypes.Dealer),
        new AddressLocation("Street 2", 3, HouseSide.Right),
        new ProceduralName("Klaus", "Berger"),
        250.0);

    [Fact]
    public void NewRun_IsOffered()
    {
        var run = new CarHaulRun(Offer);
        Assert.Equal(CarHaulPhase.Offered, run.Phase);
        Assert.Null(run.DeliveryStartGameMinute);
        Assert.Equal(0, run.DriveMinutes);
    }

    [Fact]
    public void PickUp_OnlyAtTheJobsDealer()
    {
        var run = new CarHaulRun(Offer);

        Assert.False(run.PickUp("dealer-2"));
        Assert.Equal(CarHaulPhase.Offered, run.Phase);

        Assert.True(run.PickUp("DEALER-1"));
        Assert.Equal(CarHaulPhase.PickedUp, run.Phase);
        Assert.False(run.MatchesDealerPickup("dealer-1"));
    }

    [Fact]
    public void PickUp_RejectsAnEmptyDoor()
    {
        var run = new CarHaulRun(Offer);
        Assert.False(run.PickUp(""));
    }

    [Fact]
    public void BeginDelivery_NeedsThePickUpAndStampsOnce()
    {
        var run = new CarHaulRun(Offer);

        Assert.False(run.BeginDelivery(1_000));

        Assert.True(run.PickUp("dealer-1"));
        Assert.True(run.BeginDelivery(1_010));
        Assert.Equal(1_010, run.DeliveryStartGameMinute);

        // A second DRIVE tap must not reset the leg and shrink the pay.
        Assert.False(run.BeginDelivery(1_020));
    }

    [Fact]
    public void Deliver_PricesTheLegInGameMinutes()
    {
        var run = new CarHaulRun(Offer);
        run.PickUp("dealer-1");
        run.BeginDelivery(1_010);

        Assert.True(run.Deliver(1_100));
        Assert.Equal(CarHaulPhase.Delivered, run.Phase);
        Assert.Equal(90, run.DriveMinutes);

        // Settling again is a no-op, so the page can never pay twice.
        Assert.False(run.Deliver(1_200));
        Assert.Equal(90, run.DriveMinutes);
    }

    [Fact]
    public void Deliver_WithoutAStartPaysNothing()
    {
        var run = new CarHaulRun(Offer);
        run.PickUp("dealer-1");

        Assert.True(run.Deliver(1_100));
        Assert.Equal(0, run.DriveMinutes);
    }

    [Fact]
    public void Deliver_WithReversedStampsPaysNothing()
    {
        var run = new CarHaulRun(Offer);
        run.PickUp("dealer-1");
        run.BeginDelivery(1_100);

        Assert.True(run.Deliver(1_050));
        Assert.Equal(0, run.DriveMinutes);
    }

    [Fact]
    public void Deliver_RejectedBeforePickUp()
    {
        var run = new CarHaulRun(Offer);
        Assert.False(run.Deliver(1_100));
        Assert.Equal(CarHaulPhase.Offered, run.Phase);
    }
}
