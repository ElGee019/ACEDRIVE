// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// One priced fill, as the receipt page prints it (PORTPLAN item 37).
namespace AceDrive.Core;

/// <summary>
/// A settled fill with its money side (port of Unity FuelStationScreen's result
/// block): the litres EVO added, the CR/L that was on the pump, what the button
/// actually took (<see cref="Free"/> when a Free Tank Refill card covered it),
/// and the station attendant's familiarity after the visit.
/// </summary>
public sealed record FuelReceipt(
    double Litres,
    double PricePerLitre,
    double Total,
    bool Free,
    FriendshipResult? Friendship)
{
    /// <summary>Whether the page can name a station attendant at all (a
    /// content file with no owner degrades to a receipt without the box).</summary>
    public bool HasFriendship => Friendship is { OwnerName.Length: > 0 };
}
