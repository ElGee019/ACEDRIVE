using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The care-package ledger (PORTPLAN 21): one kit per completion, the run's
/// 5-kit budget, the pickup/drop-off pair and the saved shape that resumes a
/// run which quit mid-drive.
/// </summary>
public sealed class CarePackageLedgerTests
{
    [Fact]
    public void Grant_MergesAtOneStoreAndStopsAtTheCap()
    {
        var ledger = new CarePackageLedger();

        Assert.True(ledger.Grant("groceries_store"));
        Assert.True(ledger.Grant("groceries_store"));
        Assert.Equal(2, Assert.Single(ledger.Stores).Count);
        Assert.Equal(2, ledger.Total);

        for (int i = 2; i < CarePackageLedger.MaxPackages; i++)
        {
            Assert.True(ledger.Grant("coffee_shop"));
        }

        Assert.True(ledger.IsFull);
        Assert.False(ledger.Grant("coffee_shop"));
        Assert.False(ledger.Grant(""));
        Assert.Equal(CarePackageLedger.MaxPackages, ledger.Total);
        Assert.Equal(2, ledger.Stores.Count);
    }

    [Fact]
    public void AllocateRun_CapsTheRunAndLeavesTheRestOnTheShelves()
    {
        var ledger = new CarePackageLedger();
        for (int i = 0; i < 3; i++)
        {
            ledger.Grant("groceries_store");
        }

        for (int i = 0; i < 4; i++)
        {
            ledger.Grant("coffee_shop");
        }

        IReadOnlyList<CarePackagePlacement> legs = ledger.AllocateRun();

        // Five kits in store order: all three grocery kits, then two coffee.
        Assert.Equal(2, legs.Count);
        Assert.Equal(3, legs[0].Count);
        Assert.Equal("groceries_store", legs[0].StoreId);
        Assert.Equal(2, legs[1].Count);
        Assert.Equal("coffee_shop", legs[1].StoreId);
        // The allocation is a plan, not a pickup: the shelves keep every kit.
        Assert.Equal(7, ledger.Total);
        Assert.Equal(7, ledger.Outstanding);
    }

    [Fact]
    public void Pickup_MovesKitsIntoTheCarWithoutLosingTheTotal()
    {
        var ledger = new CarePackageLedger();
        for (int i = 0; i < 3; i++)
        {
            ledger.Grant("groceries_store");
        }

        Assert.Equal(2, ledger.Pickup("groceries_store", 2));
        Assert.Equal(2, ledger.Carried);
        Assert.Equal(1, ledger.Outstanding);
        Assert.Equal(3, ledger.Total);

        // A pickup larger than the shelf moves what is there, never invents a kit.
        Assert.Equal(1, ledger.Pickup("groceries_store", 5));
        Assert.Equal(3, ledger.Carried);
        Assert.Equal(0, ledger.Outstanding);
        Assert.Empty(ledger.Stores);
    }

    [Fact]
    public void Deliver_DropsOnlyTheCarriedKits()
    {
        var ledger = new CarePackageLedger();
        for (int i = 0; i < 3; i++)
        {
            ledger.Grant("groceries_store");
        }

        for (int i = 0; i < 4; i++)
        {
            ledger.Grant("coffee_shop");
        }

        _ = ledger.Pickup("groceries_store", 3);
        _ = ledger.Pickup("coffee_shop", 2);

        Assert.Equal(5, ledger.Deliver());
        Assert.Equal(0, ledger.Carried);
        Assert.Equal(2, ledger.Total);
        Assert.Equal(2, ledger.Outstanding);
    }

    [Fact]
    public void Snapshot_ResumesTheShelvesAndTheCar()
    {
        var ledger = new CarePackageLedger();
        for (int i = 0; i < 3; i++)
        {
            ledger.Grant("groceries_store");
        }

        _ = ledger.Pickup("groceries_store", 2);

        var resumed = new CarePackageLedger(ledger.Snapshot());

        Assert.Equal(2, resumed.Carried);
        Assert.Equal(1, resumed.Outstanding);
        Assert.Equal(3, resumed.Total);
        Assert.Equal("groceries_store", Assert.Single(resumed.Stores).StoreId);
    }

    [Fact]
    public void CorruptSavedRows_AreDroppedAndNegativeKitCountsClampToZero()
    {
        var resumed = new CarePackageLedger(new CarePackageState(
            [
                new CarePackagePlacement("", 2),
                new CarePackagePlacement("coffee_shop", 0),
                new CarePackagePlacement("groceries_store", 2),
            ],
            Carried: -4));

        Assert.Equal(0, resumed.Carried);
        Assert.Equal(2, resumed.Total);
        Assert.Equal("groceries_store", Assert.Single(resumed.Stores).StoreId);
    }
}
