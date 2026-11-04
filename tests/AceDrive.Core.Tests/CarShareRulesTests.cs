using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// Unity CarShareService's balance numbers: the citizen-scaled ride count, the
/// XP one request pays (distance × class × friendship over 500, a stranger at
/// half) and the day-seeded stream that keeps a rebuilt page on the same batch.
/// </summary>
public sealed class CarShareRulesTests
{
    [Theory]
    [InlineData(500, 0, 2)]
    [InlineData(1_500, 1, 3)]
    [InlineData(2_500, 2, 5)]
    [InlineData(3_500, 3, 8)]
    public void RidesFor_StaysInsideTheBandsUnityDocuments(
        int citizens, int min, int max)
    {
        bool sawFloor = false;
        bool sawCeiling = false;
        for (int seed = 0; seed < 200; seed++)
        {
            int rides = CarShareRules.RidesFor(new Random(seed), citizens);
            Assert.InRange(rides, min, max);
            sawFloor |= rides == min;
            sawCeiling |= rides == max;
        }

        Assert.True(sawFloor, $"band {citizens} never rolled its floor {min}");
        Assert.True(sawCeiling, $"band {citizens} never rolled its ceiling {max}");
    }

    [Fact]
    public void Xp_MultipliesDistanceClassAndFriendship_DividedBy500()
    {
        // 10 km × class 4 × friendship 2 = 80 / 500.
        Assert.Equal(0.16, CarShareRules.Xp(10.0, 4, 2), precision: 10);
    }

    [Fact]
    public void Xp_CountsAStrangerAtHalf()
    {
        Assert.Equal(0.04, CarShareRules.Xp(10.0, 4, 0), precision: 10);
    }

    [Fact]
    public void Xp_IsZeroForACarTheGarageCannotName()
    {
        Assert.Equal(0.0, CarShareRules.Xp(10.0, 0, 5));
    }

    [Fact]
    public void RideMinutes_BooksTheDistanceAtTheAssumedSpeed()
    {
        Assert.Equal(20, CarShareRules.RideMinutes(10.0));
    }

    [Fact]
    public void RideMinutes_NeverBooksUnderTheMinimum()
    {
        Assert.Equal(CarShareRules.MinRideMinutes, CarShareRules.RideMinutes(0.1));
        Assert.Equal(CarShareRules.MinRideMinutes, CarShareRules.RideMinutes(0.0));
    }

    [Fact]
    public void RideMinutes_GuardsANonFiniteDistance()
    {
        Assert.Equal(CarShareRules.MinRideMinutes, CarShareRules.RideMinutes(double.NaN));
    }

    [Fact]
    public void BatchXp_SumsEveryRideOfTheDay()
    {
        Assert.Equal(0.375, CarShareRules.BatchXp(
            [Ride(leaveMinute: 500, xp: 0.125), Ride(leaveMinute: 600, xp: 0.250)]),
            precision: 10);
        Assert.Equal(0.0, CarShareRules.BatchXp([]));
    }

    [Fact]
    public void AssignmentFor_TimesTheShiftToTheLastArrival()
    {
        // 08:00 on a day whose last ride arrives at 10:10.
        EmployeeJobAssignment job = CarShareRules.AssignmentFor(
            [Ride(leaveMinute: 500, xp: 0.125), Ride(leaveMinute: 600, xp: 0.250)],
            gameMinute: 1_000_000, gameMinuteOfDay: 8 * 60);

        Assert.Equal(EmployeeJobKind.CarSharing, job.Kind);
        Assert.Equal(1_000_130, job.DueGameMinute);
        Assert.Equal(0.375, job.Xp, precision: 10);
    }

    [Fact]
    public void AssignmentFor_QuietDayHasNoFinishToArriveAt()
    {
        EmployeeJobAssignment job = CarShareRules.AssignmentFor(
            [], gameMinute: 1_000_000, gameMinuteOfDay: 8 * 60);

        Assert.Null(job.DueGameMinute);
        Assert.Equal(0.0, job.Xp);
    }

    [Fact]
    public void AssignmentFor_BatchWhoseRidesLeftHasNoFinishEither()
    {
        EmployeeJobAssignment job = CarShareRules.AssignmentFor(
            [Ride(leaveMinute: 400, xp: 0.125)],
            gameMinute: 1_000_000, gameMinuteOfDay: 8 * 60);

        Assert.Null(job.DueGameMinute);
    }

    private static CarShareRequest Ride(int leaveMinute, double xp) =>
        new("Lena Vogt", new AddressLocation("Hafenstrasse", 12, HouseNumberPlan.SideOf(12)),
            new AddressLocation("Ringstrasse", 4, HouseNumberPlan.SideOf(4)),
            4.2, leaveMinute, xp);

    [Fact]
    public void SeedFor_IsStablePerDayAndDiffersBetweenDays()
    {
        Assert.Equal(CarShareRules.SeedFor(7), CarShareRules.SeedFor(7));
        Assert.NotEqual(CarShareRules.SeedFor(7), CarShareRules.SeedFor(8));
    }
}
