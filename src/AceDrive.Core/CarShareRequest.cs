// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 22 (car-sharing passive income), port of Unity
// CarShareService's CarShareReservation. One ride on the day's schedule; the
// page paints it, the earning rule lives in CarShareRules.
namespace AceDrive.Core;

/// <summary>
/// One car-sharing request on a game day: the resident who asked, the doors
/// they travel between, how far the ride is, when the shared car leaves and
/// what completing it pays. Unity keeps the same fields on its reservation row;
/// the XP is priced when the day is planned, so the list the player reads is
/// the money the ride is worth.
/// </summary>
public sealed record CarShareRequest(
    string CustomerName,
    AddressLocation Pickup,
    AddressLocation Dropoff,
    double DistanceKm,
    int LeaveMinute,
    double Xp)
{
    /// <summary>
    /// The minute the ride reaches its drop-off. The planner books the leave
    /// time so this never passes the day's window, and the assignment reads the
    /// day's latest arrival as the minute the employee's job finishes.
    /// </summary>
    public int ArrivalMinute => LeaveMinute + CarShareRules.RideMinutes(DistanceKm);
}
