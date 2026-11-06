// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// One priced fill, as the receipt page prints it (PORTPLAN items 37/38).
namespace AceDrive.Core;

/// <summary>
/// A settled fill with its money side (port of Unity FuelStationScreen's result
/// block): the litres EVO added, the CR/L that was on the pump, what the button
/// actually took (<see cref="Free"/> when a Free Tank Refill card covered it),
/// the station attendant's familiarity after the visit, and the litres a
/// roadside repair had already paid for (PORTPLAN 38) so the page never bills
/// them twice.
/// </summary>
public sealed record FuelReceipt(
    double Litres,
    double PricePerLitre,
    double Total,
    bool Free,
    FriendshipResult? Friendship,
    double PrepaidLitres = 0.0)
{
    /// <summary>Whether the page can name a station attendant at all (a
    /// content file with no owner degrades to a receipt without the box).</summary>
    public bool HasFriendship => Friendship is { OwnerName.Length: > 0 };

    /// <summary>True when a roadside refuel covered part or all of this fill.</summary>
    public bool HasPrepaid => PrepaidLitres > 0.0;
}
