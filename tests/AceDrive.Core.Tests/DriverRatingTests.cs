using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The driver-rating value (PORTPLAN 06, Unity DriverRatingService):
/// the three skill shares, Unity's clamp and the overall average.</summary>
public sealed class DriverRatingTests
{
    [Fact]
    public void NewGame_SitsOnUnitysOverallTwenty()
    {
        DriverRating rating = DriverRating.NewGame;

        Assert.Equal(20.0, rating.Cornering);
        Assert.Equal(20.0, rating.Concentration);
        Assert.Equal(20.0, rating.OffTheLine);
        Assert.Equal(DriverRating.NewGameOverall, rating.Overall);
    }

    [Fact]
    public void Overall_IsThePlainAverage()
    {
        var rating = new DriverRating(30.0, 60.0, 90.0);

        Assert.Equal(60.0, rating.Overall, precision: 9);
    }

    [Fact]
    public void ClampStat_KeepsTheUnityBounds()
    {
        // The lower bound is exclusive: 11 is not a legal value, so a zero
        // save falls back to the smallest representable stat.
        Assert.Equal(11.01, DriverRating.ClampStat(0.0));
        Assert.Equal(11.01, DriverRating.ClampStat(11.0));
        Assert.Equal(11.01, DriverRating.ClampStat(double.NaN));
        Assert.Equal(100.0, DriverRating.ClampStat(100.0));
        Assert.Equal(100.0, DriverRating.ClampStat(250.0));
        Assert.Equal(37.5, DriverRating.ClampStat(37.5));
    }

    [Fact]
    public void FromSaved_ClampsEachSkillInPlace()
    {
        DriverRating rating = DriverRating.FromSaved(-5.0, 44.0, 500.0);

        Assert.Equal(11.01, rating.Cornering);
        Assert.Equal(44.0, rating.Concentration);
        Assert.Equal(100.0, rating.OffTheLine);
    }

    [Fact]
    public void SkillDeltas_MoveOneStatAndStayLegal()
    {
        DriverRating rating = new(50.0, 50.0, 50.0);

        Assert.Equal(51.0, rating.AddCornering(1.0).Cornering);
        Assert.Equal(51.0, rating.AddConcentration(1.0).Concentration);
        Assert.Equal(51.0, rating.AddOffTheLine(1.0).OffTheLine);
        // Only the moved stat changes.
        Assert.Equal(50.0, rating.AddCornering(1.0).Concentration);

        Assert.Equal(100.0, rating.AddCornering(80.0).Cornering);
        Assert.Equal(11.01, rating.AddCornering(-80.0).Cornering);
    }

    [Fact]
    public void SkillDeltas_IgnoreANonFiniteDelta()
    {
        // A broken event must never reset a skill to the ClampStat fallback.
        DriverRating rating = new(50.0, 50.0, 50.0);

        Assert.Equal(rating, rating.AddCornering(double.NaN));
        Assert.Equal(rating, rating.AddRandomSkill(new Random(0), double.PositiveInfinity));
    }

    [Fact]
    public void AddRandomSkill_LandsOnExactlyOneSkill()
    {
        DriverRating rating = DriverRating.NewGame;
        var rng = new Random(7);

        DriverRating moved = rating.AddRandomSkill(rng, 1.0);

        double sum = moved.Cornering + moved.Concentration + moved.OffTheLine;
        Assert.Equal(61.0, sum, precision: 9);
        Assert.Equal(1, new[] { moved.Cornering, moved.Concentration, moved.OffTheLine }
            .Count(stat => stat == 21.0));
    }
}
