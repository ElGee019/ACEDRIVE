using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Road-test board ordering (lower is better, earlier tie wins).</summary>
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
}
