using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The Cento Miglia field draw (PORTPLAN 49, port of Unity
/// RaceParticipantPicker.Pick + MilleMigliaManager's placement table): the
/// player and three non-driving rivals are shuffled into one finishing order,
/// and an empty roster still answers the solo first place.</summary>
public sealed class CentoRaceFieldTests
{
    private static readonly CentoCompetitor[] Rivals =
    [
        new("Viktor Brandl", "Alfa Romeo Giulia GTA Sprint", 42),
        new("Elodie Marchand", "Abarth 695 biposto", 51),
        new("Tommy Whitcombe", "Mazda MX-5", 33),
    ];

    [Fact]
    public void Draw_AssignsEveryPlaceOnceAndFlagsThePlayer()
    {
        CentoRaceResult result = CentoRaceField.Draw(
            Rivals, "ElGee", "Mazda MX-5", 20, new Random(1));

        Assert.Equal(4, result.Standings.Count);
        Assert.Equal(
            [1, 2, 3, 4],
            result.Standings.Select(row => row.Place).ToArray());
        CentoStanding own = Assert.Single(result.Standings, row => row.IsPlayer);
        Assert.Equal(result.PlayerPlacement, own.Place);
        Assert.Equal("ElGee", own.Name);
        Assert.Equal("Mazda MX-5", own.Car);
        Assert.Equal(20, own.DriverRating);
        Assert.Single(result.Standings, row => !row.IsPlayer && row.Name == "Viktor Brandl");
        Assert.Single(result.Standings, row => !row.IsPlayer && row.Name == "Elodie Marchand");
        Assert.Single(result.Standings, row => !row.IsPlayer && row.Name == "Tommy Whitcombe");
    }

    [Fact]
    public void Draw_KeepsTheRivalsCarsAndRatings()
    {
        CentoRaceResult result = CentoRaceField.Draw(
            Rivals, "", "", 0, new Random(7));

        CentoStanding elodie = Assert.Single(
            result.Standings, row => row.Name == "Elodie Marchand");
        Assert.Equal("Abarth 695 biposto", elodie.Car);
        Assert.Equal(51, elodie.DriverRating);
        Assert.False(elodie.IsPlayer);
    }

    [Fact]
    public void Draw_IsSeeded()
    {
        CentoRaceResult first = CentoRaceField.Draw(
            Rivals, "ElGee", "Mazda MX-5", 20, new Random(3));
        CentoRaceResult again = CentoRaceField.Draw(
            Rivals, "ElGee", "Mazda MX-5", 20, new Random(3));

        Assert.Equal(first.PlayerPlacement, again.PlayerPlacement);
        Assert.Equal(first.Standings.ToArray(), again.Standings.ToArray());
    }

    [Fact]
    public void Draw_WithNoRivalsFinishesThePlayerFirst()
    {
        CentoRaceResult result = CentoRaceField.Draw(
            [], "", "", 20, new Random(0));

        CentoStanding only = Assert.Single(result.Standings);
        Assert.True(only.IsPlayer);
        Assert.Equal(1, only.Place);
        Assert.Equal(1, result.PlayerPlacement);
    }

    [Fact]
    public void Draw_WithoutARosterLeavesOneRow()
    {
        // A hand-built rules record with no field degrades to the solo race the
        // receipt printed before the roster existed.
        CentoRaceResult result = CentoRaceField.Draw(
            null, "ElGee", "Mazda MX-5", 20, new Random(0));

        Assert.Equal(1, result.PlayerPlacement);
        Assert.Equal("ElGee", Assert.Single(result.Standings).Name);
    }

    [Fact]
    public void Draw_ReachesEveryPlaceOverSeeds()
    {
        var places = new HashSet<int>();
        for (int seed = 0; seed < 40; seed++)
        {
            places.Add(CentoRaceField.Draw(
                Rivals, "", "", 0, new Random(seed)).PlayerPlacement);
        }

        Assert.Equal([1, 2, 3, 4], places.Order().ToArray());
    }
}
