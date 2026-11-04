using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// Settling an employee delivery, ported from LATESTDRIVEBS2
/// EmployeeService.CompleteDueDeliveries: the target store is refilled to its
/// cap, the payout is one CR per unit added, and a Worker Bonus card doubles it
/// and is spent.
/// </summary>
public sealed class EmployeeDeliveryPayoutTests
{
    [Fact]
    public void Settle_RefillsTheStoreAndPaysOneCrPerUnitAdded()
    {
        var store = Store(stocked: 5);
        var (payout, city, _, _) = Create(store);

        EmployeeDeliveryPay pay = payout.Settle("store_a");

        Assert.Equal(15.0, pay.Cr, precision: 10);
        Assert.False(pay.WorkerBonusApplied);
        Assert.Equal(store.UnitCapacity, city.Live(store).StockedUnits);
    }

    [Fact]
    public void Settle_DoublesThePayoutWithAWorkerBonusCardAndSpendsIt()
    {
        var store = Store(stocked: 5);
        var (payout, _, glovebox, trunk) = Create(store, workerBonusCard: true);

        EmployeeDeliveryPay pay = payout.Settle("store_a");

        Assert.Equal(30.0, pay.Cr, precision: 10);
        Assert.True(pay.WorkerBonusApplied);
        Assert.False(glovebox.Carries(RewardCardEffect.WorkerBonus, trunk));
    }

    [Fact]
    public void Settle_AnAlreadyFullStore_PaysNothingAndKeepsTheCard()
    {
        var store = Store(stocked: 20);
        var (payout, _, glovebox, trunk) = Create(store, workerBonusCard: true);

        EmployeeDeliveryPay pay = payout.Settle("store_a");

        Assert.Equal(0.0, pay.Cr, precision: 10);
        Assert.False(pay.WorkerBonusApplied);
        Assert.True(glovebox.Carries(RewardCardEffect.WorkerBonus, trunk));
    }

    [Fact]
    public void Settle_AnUnknownStore_PaysNothing()
    {
        var (payout, _, _, _) = Create(Store(stocked: 5));

        Assert.Equal(0.0, payout.Settle("no_such_store").Cr, precision: 10);
        Assert.Equal(0.0, payout.Settle(null).Cr, precision: 10);
    }

    private static DeliveryStore Store(int stocked) =>
        new("store_a", "Test Store", "Owner", stocked, UnitCapacity: 20, PricePerUnit: 1.0);

    private static (EmployeeDeliveryPayout Payout, CityEconomy City, Glovebox Glovebox, Trunk Trunk)
        Create(DeliveryStore store, bool workerBonusCard = false)
    {
        var city = new CityEconomy([store], CityRules.Default);
        var trunk = new Trunk();
        if (workerBonusCard)
        {
            trunk.AddCard(WorkerBonusTitle, 1);
        }

        var glovebox = new Glovebox(RewardTable.Default, new DriverWallet());
        var payout = new EmployeeDeliveryPayout(
            city,
            () => new DeliveryCatalog([store], WholesaleRate: 0.7, StartCredP: 100),
            glovebox,
            trunk);
        return (payout, city, glovebox, trunk);
    }

    // The shipped card title: the trunk stores cards by name, and the payout
    // resolves the effect through the table.
    private const string WorkerBonusTitle = "WORKER BONUS";
}
