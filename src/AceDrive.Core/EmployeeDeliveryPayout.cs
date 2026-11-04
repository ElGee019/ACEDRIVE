// Settles an employee's store refill (PORTPLAN item 23), port of
// LATESTDRIVEBS2 EmployeeService.CompleteDueDeliveries: the target store is
// topped up, the payout is one CR per unit actually added, and a Worker Bonus
// card doubles it — one card per delivery, spent when the job completes.
namespace AceDrive.Core;

/// <summary>
/// The completion side of an employee delivery: refill the store the job
/// targeted and price the goods that landed. Kept apart from the assign
/// service because the two run at different moments — the assignment is made at
/// the click, the payout is only knowable when the 24 hours are up and the
/// shelves have moved since.
/// </summary>
public sealed class EmployeeDeliveryPayout
{
    private readonly CityEconomy _city;
    private readonly Func<DeliveryCatalog> _board;
    private readonly Glovebox _glovebox;
    private readonly Trunk _trunk;

    public EmployeeDeliveryPayout(
        CityEconomy city,
        Func<DeliveryCatalog> board,
        Glovebox glovebox,
        Trunk trunk)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(glovebox);
        ArgumentNullException.ThrowIfNull(trunk);

        _city = city;
        _board = board;
        _glovebox = glovebox;
        _trunk = trunk;
    }

    /// <summary>
    /// Refills the job's target and answers what it paid. A store that has left
    /// the board or is already full pays nothing: the run added no goods, so
    /// there is nothing to bank. Never throws for a missing key.
    /// </summary>
    public EmployeeDeliveryPay Settle(string? storeKey)
    {
        if (string.IsNullOrEmpty(storeKey)
            || _board().Find(storeKey!) is not { } store)
        {
            return new EmployeeDeliveryPay(0.0, WorkerBonusApplied: false);
        }

        // The units the run can add are the live shelves' deficit, priced the
        // way Unity prices them (one CR per unit), not the assignment-time
        // board row: stock moved between the click and the 24-hour arrival.
        int units = _city.Live(store).UnitsNeeded;
        if (units <= 0 || !_city.Restock([storeKey!]))
        {
            return new EmployeeDeliveryPay(0.0, WorkerBonusApplied: false);
        }

        double cr = EmployeeJobRules.DeliveryPay(units);
        RewardCard? bonusCard = _glovebox.CardFor(RewardCardEffect.WorkerBonus);
        bool bonus = bonusCard is { Amount: > 1.0 }
            && _glovebox.TryConsume(RewardCardEffect.WorkerBonus, _trunk);
        if (bonus)
        {
            cr *= bonusCard!.Amount;
        }

        return new EmployeeDeliveryPay(
            Math.Round(cr, 2, MidpointRounding.AwayFromZero), bonus);
    }
}
