using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// Road-test board ordering (lower is better, earlier tie wins), the car's own
/// board and the all-cars record book built from the same runs.
/// </summary>
public sealed class RoadTestBoardTests
{
    private static RoadTestEntry Entry(string uid, double value, string stamp) =>
        new(uid, "Player " + uid, "ks_test", value, stamp);

    [Fact]
    public void Top_SortsLowerValuesFirst()
    {
        List<RoadTestEntry> rows = RoadTestBoard.Top(
        [
            Entry("b", 7.9, "2026-01-02T00:00:00Z"),
            Entry("a", 5.4, "2026-01-01T00:00:00Z"),
            Entry("c", 6.1, "2026-01-03T00:00:00Z"),
        ], 3);

        Assert.Equal(["a", "c", "b"], rows.Select(row => row.Uid));
    }

    [Fact]
    public void Top_EqualValue_EarlierStampWins()
    {
        List<RoadTestEntry> rows = RoadTestBoard.Top(
        [
            Entry("later", 5.0, "2026-02-01T00:00:00Z"),
            Entry("earlier", 5.0, "2026-01-01T00:00:00Z"),
        ], 2);

        Assert.Equal("earlier", rows[0].Uid);
    }

    [Fact]
    public void Top_CapsAtTheRequestedCount()
    {
        List<RoadTestEntry> rows = RoadTestBoard.Top(
        [
            Entry("a", 1, "x"),
            Entry("b", 2, "x"),
            Entry("c", 3, "x"),
        ], 2);

        Assert.Equal(2, rows.Count);
        Assert.Equal("a", rows[0].Uid);
    }

    [Fact]
    public void Standings_KeepsTheTopSliceAndMarksTheOwnRow()
    {
        List<RoadTestStanding> rows = RoadTestBoard.Standings(
        [
            Entry("a", 5.1, "x"),
            Entry("me", 5.8, "x"),
            Entry("c", 6.4, "x"),
        ], ownUid: "me", topCount: 10);

        Assert.Equal([1, 2, 3], rows.Select(row => row.Position));
        Assert.Equal([false, true, false], rows.Select(row => row.IsOwn));
    }

    [Fact]
    public void Standings_AppendsTheOwnRowWithItsTruePosition()
    {
        List<RoadTestStanding> rows = RoadTestBoard.Standings(
        [
            Entry("a", 4.1, "x"),
            Entry("b", 4.4, "x"),
            Entry("c", 4.9, "x"),
            Entry("me", 9.9, "x"),
        ], ownUid: "me", topCount: 2);

        Assert.Equal([1, 2, 4], rows.Select(row => row.Position));
        Assert.True(rows[^1].IsOwn);
        Assert.Equal("Player me", rows[^1].PlayerName);
    }

    [Fact]
    public void Standings_WithoutAKnownUid_ReturnsTheTopSliceOnly()
    {
        List<RoadTestStanding> rows = RoadTestBoard.Standings(
        [Entry("a", 4.1, "x"), Entry("b", 4.4, "x")], ownUid: null, topCount: 10);

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, row => row.IsOwn);
    }

    [Fact]
    public void KeepLocalRow_InsertsTheUnpushedRunWhereItsValueBelongs()
    {
        List<RoadTestEntry> rows = RoadTestBoard.KeepLocalRow(
        [
            Entry("a", 4.1, "x"),
            Entry("b", 6.4, "x"),
        ], Entry("me", 5.2, "x"));

        Assert.Equal(["a", "me", "b"], rows.Select(row => row.Uid));
    }

    [Fact]
    public void KeepLocalRow_DoesNotDuplicateARunTheServerAlreadyHas()
    {
        List<RoadTestEntry> rows = RoadTestBoard.KeepLocalRow(
        [Entry("me", 5.2, "x")], Entry("me", 5.2, "x"));

        Assert.Single(rows);
    }

    [Fact]
    public void KeepLocalRow_ShowsAnImprovementOverTheStoredBestAtOnce()
    {
        List<RoadTestEntry> rows = RoadTestBoard.KeepLocalRow(
        [
            Entry("a", 4.1, "x"),
            Entry("me", 6.4, "x"),
        ], Entry("me", 5.2, "x"));

        // The push may not have landed yet: the page shows the run just driven,
        // at the place its value earns, instead of the stored slower best.
        Assert.Equal(["a", "me"], rows.Select(row => row.Uid));
        Assert.Equal(5.2, rows[1].Value, 6);
    }

    [Fact]
    public void KeepLocalRow_KeepsTheStoredBestWhenTheRunIsSlower()
    {
        List<RoadTestEntry> rows = RoadTestBoard.KeepLocalRow(
        [Entry("me", 5.2, "x")], Entry("me", 7.9, "x"));

        Assert.Equal(5.2, Assert.Single(rows).Value, 6);
    }

    [Fact]
    public void BestPerCar_KeepsOneRowPerCarWithItsBestValue()
    {
        List<RoadTestEntry> rows = RoadTestBoard.BestPerCar(
        [
            CarEntry("uid-1", "ks_a", 6.4, "x"),
            CarEntry("uid-2", "ks_b", 5.1, "x"),
            CarEntry("uid-3", "ks_a", 4.2, "x"),
        ]);

        // One row per car, that car's best run, both boards best first.
        Assert.Equal(["ks_a", "ks_b"], rows.Select(row => row.CarKey));
        Assert.Equal(4.2, rows[0].Value, 6);
        Assert.Equal("uid-3", rows[0].Uid);
    }

    [Fact]
    public void BestPerCar_DropsRunsThatNameNoCar()
    {
        List<RoadTestEntry> rows = RoadTestBoard.BestPerCar(
        [
            CarEntry("uid-1", string.Empty, 0.5, "x"),
            CarEntry("uid-2", "ks_b", 5.1, "x"),
        ]);

        Assert.Equal(["ks_b"], rows.Select(row => row.CarKey));
    }

    [Fact]
    public void KeepLocalCar_ReplacesTheStoredBestOnlyWhenTheRunBeatsIt()
    {
        RoadTestEntry[] stored = [CarEntry("uid-1", "ks_a", 6.4, "x")];

        List<RoadTestEntry> slower = RoadTestBoard.KeepLocalCar(
            stored, CarEntry("me", "ks_a", 7.9, "x"));
        List<RoadTestEntry> faster = RoadTestBoard.KeepLocalCar(
            stored, CarEntry("me", "ks_a", 5.2, "x"));

        Assert.Equal(6.4, Assert.Single(slower).Value, 6);
        Assert.Equal("me", Assert.Single(faster).Uid);
    }

    [Fact]
    public void KeepLocalCar_InsertsACarTheBookHasNeverSeenWhereItsValueBelongs()
    {
        List<RoadTestEntry> rows = RoadTestBoard.KeepLocalCar(
        [
            CarEntry("uid-1", "ks_a", 4.1, "x"),
            CarEntry("uid-2", "ks_b", 6.4, "x"),
        ], CarEntry("me", "ks_c", 5.2, "x"));

        Assert.Equal(["ks_a", "ks_c", "ks_b"], rows.Select(row => row.CarKey));
    }

    private static RoadTestEntry CarEntry(string uid, string car, double value, string stamp) =>
        new(uid, "Player " + uid, car, value, stamp);
}
