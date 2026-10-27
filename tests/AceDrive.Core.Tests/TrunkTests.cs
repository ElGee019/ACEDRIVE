using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The trunk's keepsakes (ToWhereIsWilly.md §3.1, Unity's 0-volume
/// glove-compartment certificate): a kept item adds a stack that never
/// consumes capacity and survives the snapshot/load round trip, while the
/// loader still drops the rows it always dropped.
/// </summary>
public sealed class TrunkTests
{
    [Fact]
    public void AddKeepsake_AddsAZeroLitreStack()
    {
        var trunk = new Trunk();

        trunk.AddKeepsake("Bring-Them-Home Certificate Week 3 1924", 1);

        TrunkItem item = Assert.Single(trunk.Items);
        Assert.Equal("Bring-Them-Home Certificate Week 3 1924", item.Name);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(0.0, item.UnitVolume, 6);
        Assert.True(trunk.Contains(item.Name));
    }

    [Fact]
    public void AddKeepsake_NeverConsumesTrunkCapacity()
    {
        var trunk = new Trunk();
        trunk.Add("Crate", 1, 100.0);

        trunk.AddKeepsake("Certificate", 1);

        Assert.Equal(100.0, trunk.UsedLitres, 6);
        Assert.Equal(0.0, trunk.Remaining(100.0), 6);
    }

    [Fact]
    public void AddKeepsake_MergesRepeatsAndIgnoresUnusableRows()
    {
        var trunk = new Trunk();

        trunk.AddKeepsake("Certificate", 1);
        trunk.AddKeepsake("Certificate", 2);
        trunk.AddKeepsake("", 1);
        trunk.AddKeepsake("Certificate", 0);

        TrunkItem item = Assert.Single(trunk.Items);
        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public void SnapshotLoad_RoundTripsAZeroVolumeKeepsake()
    {
        var trunk = new Trunk();
        trunk.AddKeepsake("Certificate", 1);

        var resumed = new Trunk(trunk.Snapshot());

        TrunkItem item = Assert.Single(resumed.Items);
        Assert.Equal("Certificate", item.Name);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(0.0, item.UnitVolume, 6);
        Assert.Equal(0.0, resumed.UsedLitres, 6);
    }

    [Fact]
    public void Load_StillDropsNegativeAndNonFiniteVolumes()
    {
        var resumed = new Trunk(
        [
            new TrunkItem("Negative", -1.0) { Quantity = 1 },
            new TrunkItem("NaN", double.NaN) { Quantity = 1 },
            new TrunkItem("Good", 0.0) { Quantity = 1 },
        ]);

        TrunkItem item = Assert.Single(resumed.Items);
        Assert.Equal("Good", item.Name);
    }

    [Fact]
    public void Add_StillRejectsAZeroVolumeStack()
    {
        var trunk = new Trunk();

        // The ordinary purchase path keeps its own guard: only keepsakes are
        // allowed to weigh nothing.
        trunk.Add("Crate", 1, 0.0);

        Assert.Empty(trunk.Items);
    }
}
