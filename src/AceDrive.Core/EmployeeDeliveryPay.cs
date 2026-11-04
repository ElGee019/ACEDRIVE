// What one settled employee delivery paid (PORTPLAN item 23): the CR the
// collect banks and whether a Worker Bonus card doubled it, so the page can
// tell the player what happened without re-deriving either number.
namespace AceDrive.Core;

/// <summary>
/// One completed store refill: the CR its added units pay, after the Worker
/// Bonus card's multiplier when that card was spent on it.
/// </summary>
public sealed record EmployeeDeliveryPay(double Cr, bool WorkerBonusApplied);
