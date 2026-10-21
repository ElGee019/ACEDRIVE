using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The speed-challenge fine rule (PORTPLAN item 11, Phase C):
/// radius and limit are inclusive/exclusive exactly at the edges, declined
/// challenges never fine and a patrol charges once.</summary>
public sealed class SpeedChallengeFinesTests
{
    private const double TrackLength = 1929.0;

    private static PolicePatrol Patrol(
        int id = 1, double offset = 100.0, double radius = 30.0, int limit = 50) =>
        new(id, new AddressLocation("Street 1", 1, HouseSide.Right),
            offset, radius, limit);

    private static double PositionAt(double offset) => offset / TrackLength;

    [Fact]
    public void InsideRadiusAboveTheLimit_FinesThePatrol()
    {
        PolicePatrol patrol = Patrol();

        Assert.Same(patrol, SpeedChallengeFines.Check(
            [patrol], PositionAt(110.0), TrackLength, 51.0, new HashSet<int>(), agreed: true));
    }

    [Fact]
    public void ExactlyAtTheLimit_IsLegal()
    {
        Assert.Null(SpeedChallengeFines.Check(
            [Patrol()], PositionAt(100.0), TrackLength, 50.0,
            new HashSet<int>(), agreed: true));
    }

    [Fact]
    public void OutsideTheRadius_IsNotCaught()
    {
        Assert.Null(SpeedChallengeFines.Check(
            [Patrol()], PositionAt(131.0), TrackLength, 80.0,
            new HashSet<int>(), agreed: true));
    }

    [Fact]
    public void WrapAroundTheStartLine_IsStillInsideTheRadius()
    {
        PolicePatrol patrol = Patrol(offset: 10.0);

        Assert.Same(patrol, SpeedChallengeFines.Check(
            [patrol], PositionAt(TrackLength - 10.0), TrackLength, 80.0,
            new HashSet<int>(), agreed: true));
    }

    [Fact]
    public void DeclinedChallenge_NeverFines()
    {
        Assert.Null(SpeedChallengeFines.Check(
            [Patrol()], PositionAt(100.0), TrackLength, 200.0,
            new HashSet<int>(), agreed: false));
    }

    [Fact]
    public void AlreadyFinedPatrol_DoesNotChargeAgain()
    {
        Assert.Null(SpeedChallengeFines.Check(
            [Patrol()], PositionAt(100.0), TrackLength, 80.0,
            new HashSet<int> { 1 }, agreed: true));
    }

    [Fact]
    public void FirstUnfinedPatrolInReach_Wins()
    {
        PolicePatrol second = Patrol(id: 2);
        PolicePatrol? caught = SpeedChallengeFines.Check(
            [Patrol(), second], PositionAt(100.0), TrackLength, 80.0,
            new HashSet<int> { 1 }, agreed: true);

        Assert.Same(second, caught);
    }

    [Fact]
    public void InvalidTelemetry_NoFine()
    {
        Assert.Null(SpeedChallengeFines.Check(
            [Patrol()], double.NaN, TrackLength, 80.0,
            new HashSet<int>(), agreed: true));
        Assert.Null(SpeedChallengeFines.Check(
            [Patrol()], PositionAt(100.0), TrackLength, double.NaN,
            new HashSet<int>(), agreed: true));
        Assert.Null(SpeedChallengeFines.Check(
            [Patrol()], PositionAt(100.0), double.NaN, 80.0,
            new HashSet<int>(), agreed: true));
    }
}
