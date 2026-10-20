using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Road-test ledger (PORTPLAN item 49): draws without replacement,
/// resets only once every car ran, and keeps the running pick across the
/// snapshots a restart reopens.</summary>
public sealed class RoadTestLedgerTests
{
    [Fact]
    public void FirstPick_DeterministicForSeed()
    {
        var first = new RoadTestLedger(TestRoadCars.Pool, seed: 7);
        var second = new RoadTestLedger(TestRoadCars.Pool, seed: 7);
        Assert.Equal(second.Current?.Id, first.Current?.Id);
        Assert.Contains(TestRoadCars.Pool, c => c.Id == first.Current?.Id);
    }

    [Fact]
    public void Picks_NeverRepeatUntilExhausted()
    {
        var ledger = new RoadTestLedger(TestRoadCars.Pool, seed: 7);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < TestRoadCars.Pool.Count; i++)
        {
            Assert.NotNull(ledger.Current);
            Assert.True(seen.Add(ledger.Current.Id));
            if (i < TestRoadCars.Pool.Count - 1)
            {
                ledger.MarkTested();
            }
        }
        Assert.Equal(TestRoadCars.Pool.Count, seen.Count);
    }

    [Fact]
    public void ExhaustedPool_ResetsAndPicksAgain()
    {
        var ledger = new RoadTestLedger(TestRoadCars.Pool, seed: 7);
        for (int i = 0; i < TestRoadCars.Pool.Count; i++)
        {
            ledger.MarkTested();
        }
        ledger.MarkTested();
        Assert.NotNull(ledger.Current);
        Assert.Single(ledger.TestedIds);
    }

    [Fact]
    public void Resume_KeepsTheSavedPick()
    {
        // A new session with an unrelated seed: the banked pick is reopened,
        // not redrawn, so the road-test car never changes between sessions.
        var first = TestRoadCars.Ledger(seed: 7);
        RoadTestSnapshot saved = first.Snapshot();

        var restarted = TestRoadCars.Ledger(seed: 99, saved: saved);
        Assert.Equal(first.Current?.Id, restarted.Current?.Id);
        Assert.Equal(saved.TestedIds, restarted.TestedIds);
    }

    [Fact]
    public void Resume_DropsIdsThePoolNoLongerHolds()
    {
        // cars.json edited between sessions: a vanished pick falls through to
        // a fresh draw, and a stale bank never hides a live car.
        var saved = new RoadTestSnapshot("gone_from_the_pool", ["ks_mazda_mx5_na"]);
        var ledger = TestRoadCars.Ledger(seed: 7, saved: saved);

        Assert.NotNull(ledger.Current);
        Assert.NotEqual("gone_from_the_pool", ledger.Current!.Id);
        Assert.DoesNotContain(ledger.Current.Id, ledger.TestedIds);
        Assert.Contains(ledger.TestedIds, id => id == "ks_mazda_mx5_na");
    }

    [Fact]
    public void MarkTested_IsTheOnlyRotation()
    {
        // Nothing short of a driven car moves the pick: snapshotting, the way
        // every session start does, keeps the same one.
        var ledger = TestRoadCars.Ledger(seed: 7);
        string running = ledger.Current!.Id;

        Assert.Equal(running, ledger.Snapshot().CarId);
        Assert.Equal(running, TestRoadCars.Ledger(seed: 5, saved: ledger.Snapshot()).Current?.Id);

        ledger.MarkTested();
        Assert.NotEqual(running, ledger.Current!.Id);
    }

    [Fact]
    public void EmptyPool_StaysNull()
    {
        var ledger = new RoadTestLedger([], seed: 1);
        Assert.Null(ledger.Current);
        ledger.MarkTested();
        Assert.Null(ledger.Current);
        Assert.Empty(ledger.TestedIds);
    }

    [Fact]
    public void Variants_RotateAndBankSeparately()
    {
        // One folder, two showroom presets (PORTPLAN 62): each row is its own
        // road test; banking the base never skips the M Performance.
        var pool = new List<CarSpec>
        {
            new("ks_bmw_m2_coupe", 2023, "BMW", "m2 g87 coupe"),
            new("ks_bmw_m2_coupe", 2023, "BMW", "m2 g87 coupe m performance") { Preset = "preset_m2_mperf_1" },
            new("ks_mazda_mx5_na", 1990, "Mazda", "MX-5 NA"),
        };
        var ledger = new RoadTestLedger(pool, seed: 3);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < pool.Count; i++)
        {
            Assert.NotNull(ledger.Current);
            Assert.True(seen.Add(ledger.Current.Key));
            ledger.MarkTested();
        }

        Assert.Equal(pool.Count, seen.Count);
        Assert.Contains("ks_bmw_m2_coupe", seen);
        Assert.Contains("ks_bmw_m2_coupe#preset_m2_mperf_1", seen);
    }

    [Fact]
    public void Resume_VariantKey_ReopensThatVariant()
    {
        var pool = new List<CarSpec>
        {
            new("ks_bmw_m2_coupe", 2023, "BMW", "m2 g87 coupe") { Preset = "preset_m2_mech_1" },
            new("ks_bmw_m2_coupe", 2023, "BMW", "m2 g87 coupe m performance") { Preset = "preset_m2_mperf_1" },
        };
        var ledger = new RoadTestLedger(
            pool, seed: 7, new RoadTestSnapshot("ks_bmw_m2_coupe#preset_m2_mperf_1", []));
        Assert.Equal("preset_m2_mperf_1", ledger.Current?.Preset);
    }

    [Fact]
    public void Resume_FolderKey_ResolvesNullPresetRow()
    {
        // Pre-variant roadtest.json: a plain folder id still opens the row
        // whose Key is that id (the null-preset row), old bank included.
        var pool = new List<CarSpec>
        {
            new("ks_bmw_m2_coupe", 2023, "BMW", "m2 g87 coupe"),
            new("ks_bmw_m2_coupe", 2023, "BMW", "m2 g87 coupe m performance") { Preset = "preset_m2_mperf_1" },
        };
        var ledger = new RoadTestLedger(pool, seed: 7, new RoadTestSnapshot(
            "ks_bmw_m2_coupe", ["ks_bmw_m2_coupe#preset_m2_mperf_1"]));
        Assert.Equal("m2 g87 coupe", ledger.Current?.Model);
        Assert.Contains("ks_bmw_m2_coupe#preset_m2_mperf_1", ledger.TestedIds);
    }
}
