using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Closed-lap arithmetic behind the job guidance: offsets, the
/// shorter way around and the arrival radius (wraps the start line).</summary>
public sealed class LapGeometryTests
{
    private const double Length = 1929.0;

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.25, 482.25)]
    [InlineData(0.5, 964.5)]
    [InlineData(1.0, 1929.0)]
    public void Offset_MapsNormalisedPositionToMetres(double position, double expected)
    {
        Assert.Equal(expected, LapGeometry.OffsetMeters(position, Length), 3);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Offset_InvalidPositionOrLength_AnswersNaN(double position)
    {
        Assert.True(double.IsNaN(LapGeometry.OffsetMeters(position, Length)));
        Assert.True(double.IsNaN(LapGeometry.OffsetMeters(0.5, 0.0)));
        Assert.True(double.IsNaN(LapGeometry.OffsetMeters(0.5, double.NaN)));
    }

    [Fact]
    public void Distance_TakesTheShorterWayAroundTheStartLine()
    {
        // 10 m and length - 10 m are 20 m apart, not 1,909 m.
        Assert.Equal(20.0, LapGeometry.DistanceMeters(10.0, Length - 10.0, Length), 3);
        Assert.Equal(30.0, LapGeometry.DistanceMeters(0.0, 30.0, Length), 3);
        Assert.Equal(0.0, LapGeometry.DistanceMeters(100.0, 100.0, Length), 3);
    }

    [Fact]
    public void Distance_InvalidInputs_AnswersInfinity()
    {
        Assert.True(double.IsPositiveInfinity(
            LapGeometry.DistanceMeters(double.NaN, 10.0, Length)));
        Assert.True(double.IsPositiveInfinity(
            LapGeometry.DistanceMeters(10.0, 20.0, 0.0)));
    }

    [Fact]
    public void Arrived_WrapsAroundTheLineAndHonoursTheRadius()
    {
        // A door at 10 m is inside 25 m of a car just before the line.
        Assert.True(LapGeometry.IsArrived(0.999, 10.0, Length, 25.0));
        Assert.True(LapGeometry.IsArrived(0.0, 10.0, Length, 25.0));
        Assert.False(LapGeometry.IsArrived(0.01, 100.0, Length, 25.0));
    }

    [Fact]
    public void Arrived_InvalidRadiusOrPosition_NeverMatches()
    {
        Assert.False(LapGeometry.IsArrived(0.5, 0.0, Length, -1.0));
        Assert.False(LapGeometry.IsArrived(0.5, 0.0, Length, double.NaN));
        Assert.False(LapGeometry.IsArrived(double.NaN, 0.0, Length, 25.0));
        Assert.False(LapGeometry.IsArrived(1.5, 0.0, Length, 25.0));
    }
}
