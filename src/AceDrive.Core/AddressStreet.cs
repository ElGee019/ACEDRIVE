// Placeholder free-roam map data (PORTPLAN Step 1): a named stretch of the
// lap. Fractions are relative to the start/finish line (0.0) and the lap end
// (1.0), so the same street table can follow a future real map without moving
// any view code.
namespace AceDrive.Core;

/// <summary>One named street on the placeholder map.</summary>
public sealed record AddressStreet(
    string Id,
    string Name,
    double FromFraction,
    double ToFraction);
