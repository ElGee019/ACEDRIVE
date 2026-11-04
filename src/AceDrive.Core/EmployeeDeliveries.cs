// The employees' delivery work (PORTPLAN item 23), port of LATESTDRIVEBS2
// EmployeeService.AssignDelivery: one store that needs stock is refilled by an
// employee, and the refill always lands exactly 24 hours after the click.
namespace AceDrive.Core;

/// <summary>
/// Puts an employee on a store refill. The target is drawn from the live board
/// — a store at most 80 % full while one exists, otherwise any store with room
/// — and the job is timed at exactly <see cref="EmployeeJobRules.DeliveryMinutes"/>.
/// The payout is not known yet: it is one CR per unit the refill actually adds,
/// so <see cref="EmployeeDeliveryPayout"/> settles it when the job completes.
/// </summary>
public sealed class EmployeeDeliveries
{
    private readonly EmployeeAssignments _assignments;
    private readonly GameClock _game;
    private readonly Func<IReadOnlyList<DeliveryStore>> _liveStores;
    private readonly Random _rng;

    public EmployeeDeliveries(
        EmployeeAssignments assignments,
        GameClock game,
        Func<IReadOnlyList<DeliveryStore>> liveStores,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(liveStores);
        ArgumentNullException.ThrowIfNull(rng);

        _assignments = assignments;
        _game = game;
        _liveStores = liveStores;
        _rng = rng;
    }

    /// <summary>
    /// Assigns the refill and answers true, or answers false (assigning
    /// nothing) when every store is already full: a run that can add no stock
    /// would pay nothing.
    /// </summary>
    public bool Assign(string? employeeName)
    {
        if (PickStore() is not { } store)
        {
            return false;
        }

        _assignments.Assign(employeeName, new EmployeeJobAssignment(
            EmployeeJobKind.DeliveryJobs,
            _game.GameMinute + EmployeeJobRules.DeliveryMinutes,
            StoreKey: store.Key));
        return true;
    }

    // Unity's eligibility order: the stores that actually need stock first,
    // then any store with room. Two counting passes over the (small) board, so
    // the draw stays a cold path and allocates nothing.
    private DeliveryStore? PickStore()
    {
        IReadOnlyList<DeliveryStore> stores = _liveStores();
        int preferredCount = 0;
        int refillableCount = 0;
        foreach (DeliveryStore store in stores)
        {
            if (!EmployeeJobRules.CanRefill(store))
            {
                continue;
            }

            refillableCount++;
            if (EmployeeJobRules.IsRefillPreferred(store))
            {
                preferredCount++;
            }
        }

        if (refillableCount == 0)
        {
            return null;
        }

        bool preferNeedy = preferredCount > 0;
        int target = _rng.Next(preferNeedy ? preferredCount : refillableCount);
        foreach (DeliveryStore store in stores)
        {
            if (!EmployeeJobRules.CanRefill(store)
                || (preferNeedy && !EmployeeJobRules.IsRefillPreferred(store)))
            {
                continue;
            }

            if (target-- == 0)
            {
                return store;
            }
        }

        return null;
    }
}
