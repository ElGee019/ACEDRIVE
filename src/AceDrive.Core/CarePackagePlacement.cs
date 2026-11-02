// The care-package stock one store holds (PORTPLAN item 21, Unity
// CarePackagePoiEntry): pure data so the ledger and the receipt can name a
// pickup without touching the POI directory.
namespace AceDrive.Core;

/// <summary>How many care kits wait at one store for pickup.</summary>
public sealed record CarePackagePlacement(string StoreId, int Count);
