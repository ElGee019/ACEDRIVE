// The care-package grant's persisted half (PORTPLAN item 21, Unity
// PlayerData.carePackages + carePackagePois): every store still holding kits
// plus the kits already in the car, so a restart mid-run resumes at the
// community centre instead of losing what was picked up.
namespace AceDrive.Core;

/// <summary>The care-package ledger as the driver file stores it. Unplaced is
/// the debt a completion earned while no shop door could take the kit; it is
/// placed on a later sync instead of being lost with the session.</summary>
public sealed record CarePackageState(
    IReadOnlyList<CarePackagePlacement>? Stores = null,
    int Carried = 0,
    int Unplaced = 0);
