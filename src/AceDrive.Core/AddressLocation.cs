// One resolved point on the placeholder free-roam address map.
namespace AceDrive.Core;

/// <summary>
/// The street and house number behind a normalised lap position. Pure data:
/// the HUD formats it, the resolver owns the geometry. The side is not shown
/// today; it is the data the future marker network places a house from.
///
/// A door's identity is the street name (trimmed, ignoring case) plus the house
/// number — the side derives from the number, so it is not part of it. Keeping
/// the rule here means the directory, the resident book and every draw agree on
/// when two addresses are the same house, instead of each comparing strings.
/// </summary>
public readonly record struct AddressLocation(
    string StreetName, int HouseNumber, HouseSide Side)
{
    /// <summary>
    /// True when both name the same door. Unlike record equality, the side is
    /// ignored and the street compares trimmed and case-insensitively (a data
    /// file and a save may spell it differently).
    /// </summary>
    public bool Matches(AddressLocation other) =>
        HouseNumber == other.HouseNumber
        && string.Equals(
            Trimmed(StreetName), Trimmed(other.StreetName), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The dictionary key behind <see cref="Matches"/>, for owners that need a
    /// hash lookup per address (the resident book). Use with
    /// <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </summary>
    public string DoorKey => $"{Trimmed(StreetName)}|{HouseNumber}";

    private static string Trimmed(string? street) => (street ?? string.Empty).Trim();
}
