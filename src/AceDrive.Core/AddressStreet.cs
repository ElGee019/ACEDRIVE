// Placeholder free-roam map data (PORTPLAN Step 1): a named stretch of the
// lap. Fractions are relative to the start/finish line (0.0) and the lap end
// (1.0), so the same street table can follow a future real map without moving
// any view code.
namespace AceDrive.Core;

/// <summary>
/// One named street on the placeholder map. <paramref name="Fictional"/> marks
/// an authored street that exists only as a label on the lap (the street pool
/// is fixed content: nothing fetches or grows it at runtime). It can be
/// discovered and drawn on, but it does not grow the base area the zone and the
/// starter bonus seed from.
/// </summary>
public sealed record AddressStreet(
    string Id,
    string Name,
    double FromFraction,
    double ToFraction,
    bool Fictional = false);
