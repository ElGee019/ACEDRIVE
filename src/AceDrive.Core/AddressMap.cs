// Placeholder free-roam address data (PORTPLAN Step 1 + Career Start Screen 1).
// The shipped Values are the test-shell fallback and the deterministic fixture
// for tests; Data/address_map.json is the source of truth.
namespace AceDrive.Core;

/// <summary>
/// Fixed address fields plus the placeholder track geometry. Street fractions
/// and the 30 m house spacing are data, never literals in the ViewModel.
/// </summary>
public sealed record AddressMap(
    string Country,
    string City,
    double TrackLengthMeters,
    double HouseSpacingMeters,
    IReadOnlyList<AddressStreet> Streets)
{
    /// <summary>
    /// Brands Hatch Indy as the stand-in free-roam map: Street 1 covers the
    /// first half of the lap, Street 2 the second half. 1,929 m is the
    /// circuit's official length; the last few metres of each street simply
    /// have no further house number. The shipped data file splits the same lap
    /// into thirty-two 3-house streets so every address is reachable; this
    /// two-street pair is only the fallback when that file is missing.
    /// </summary>
    public static readonly AddressMap Default = new(
        "Germany",
        "Brands",
        1929.0,
        30.0,
        [new AddressStreet("street-1", "Street 1", 0.0, 0.5),
         new AddressStreet("street-2", "Street 2", 0.5, 1.0)]);
}
