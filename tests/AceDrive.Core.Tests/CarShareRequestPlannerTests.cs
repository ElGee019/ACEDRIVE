using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The assign page's daily car-sharing batch: Unity CarShareService's ride
/// count, a resident and two zone doors per ride, a leave time that never
/// double-books the shared car, and an XP price that follows the friendship
/// level and the driver's diminishing returns.
/// </summary>
public sealed class CarShareRequestPlannerTests
{
    private static readonly AddressMap Map = AddressMap.Default;

    [Fact]
    public void NoResidents_NoRequests()
    {
        var planner = new CarShareRequestPlanner(
            TaxiTestData.FullZone(Map), EmptyResidents(), _ => 0);

        Assert.Empty(planner.ForDay(gameDay: 1, totalCitizens: 5_000, carClass: 4, currentXp: 0.0));
    }

    [Fact]
    public void SameDay_ReplaysTheSameBatch()
    {
        CarShareRequestPlanner planner = Planner();

        IReadOnlyList<CarShareRequest> first =
            planner.ForDay(gameDay: 42, totalCitizens: 5_000, carClass: 4, currentXp: 0.0);
        IReadOnlyList<CarShareRequest> second =
            planner.ForDay(gameDay: 42, totalCitizens: 5_000, carClass: 4, currentXp: 0.0);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void EveryRequest_TakesTwoDifferentDoorsAndPaysXp()
    {
        IReadOnlyList<CarShareRequest> requests =
            Planner().ForDay(gameDay: 5, totalCitizens: 5_000, carClass: 4, currentXp: 0.0);

        Assert.NotEmpty(requests);
        foreach (CarShareRequest request in requests)
        {
            Assert.False(request.Pickup.Matches(request.Dropoff));
            Assert.True(request.DistanceKm > 0.0);
            Assert.True(request.Xp > 0.0);
            Assert.False(string.IsNullOrWhiteSpace(request.CustomerName));
        }
    }

    [Fact]
    public void Rides_FitTheDailyWindowAndNeverOverlap()
    {
        CarShareRequestPlanner planner = Planner();

        for (int day = 0; day < 20; day++)
        {
            IReadOnlyList<CarShareRequest> requests =
                planner.ForDay(day, totalCitizens: 8_000, carClass: 4, currentXp: 0.0);

            int previousArrival = -1;
            foreach (CarShareRequest request in requests)
            {
                int driveMinutes = DriveMinutes(request.DistanceKm);
                Assert.InRange(
                    request.LeaveMinute,
                    CarShareRules.EarliestLeaveMinute,
                    CarShareRules.LatestEndMinute - 1);
                Assert.True(
                    request.LeaveMinute + driveMinutes <= CarShareRules.LatestEndMinute,
                    $"day {day}: a ride ends after the window");
                Assert.Equal(request.LeaveMinute + driveMinutes, request.ArrivalMinute);
                if (previousArrival >= 0)
                {
                    Assert.True(
                        request.LeaveMinute >= previousArrival,
                        $"day {day}: the shared car is double-booked");
                }

                previousArrival = request.ArrivalMinute;
            }
        }
    }

    [Fact]
    public void AFriendlierCustomer_PaysMoreForTheSameRide()
    {
        IReadOnlyList<CarShareRequest> strangerRides =
            Planner(_ => 0).ForDay(gameDay: 9, totalCitizens: 5_000, carClass: 4, currentXp: 0.0);
        IReadOnlyList<CarShareRequest> friendRides =
            Planner(_ => 5).ForDay(gameDay: 9, totalCitizens: 5_000, carClass: 4, currentXp: 0.0);

        Assert.Equal(strangerRides.Count, friendRides.Count);
        Assert.True(TotalXp(friendRides) > TotalXp(strangerRides));
    }

    [Fact]
    public void MoreDriverXp_DiminishesTheEarnings()
    {
        CarShareRequestPlanner planner = Planner();

        IReadOnlyList<CarShareRequest> fresh =
            planner.ForDay(gameDay: 9, totalCitizens: 5_000, carClass: 4, currentXp: 0.0);
        IReadOnlyList<CarShareRequest> veteran =
            planner.ForDay(gameDay: 9, totalCitizens: 5_000, carClass: 4, currentXp: 10_000.0);

        Assert.True(TotalXp(veteran) < TotalXp(fresh));
    }

    private static CarShareRequestPlanner Planner(Func<string, int>? friendship = null) =>
        new(TaxiTestData.FullZone(Map), Residents(), friendship ?? (_ => 0));

    private static ResidentBook Residents()
    {
        var book = new ResidentBook(
            new FakeResidentStore(),
            new ProceduralNameGenerator(NamePoolTable.Default, seed: 0),
            new NullLogger());
        for (int number = 1; number <= 3; number++)
        {
            book.GetOrCreate(new AddressLocation(
                Map.Streets[0].Name, number, HouseNumberPlan.SideOf(number)));
        }

        return book;
    }

    private static ResidentBook EmptyResidents() =>
        new(
            new FakeResidentStore(),
            new ProceduralNameGenerator(NamePoolTable.Default, seed: 0),
            new NullLogger());

    // The planner's own ride length, recomputed so the window check tests the
    // published numbers instead of trusting the code under test.
    private static int DriveMinutes(double distanceKm) =>
        Math.Max(
            CarShareRules.MinRideMinutes,
            (int)Math.Round(distanceKm / CarShareRules.AverageDriveSpeedKmH * 60.0));

    private static double TotalXp(IReadOnlyList<CarShareRequest> requests)
    {
        double total = 0.0;
        foreach (CarShareRequest request in requests)
        {
            total += request.Xp;
        }

        return total;
    }
}
