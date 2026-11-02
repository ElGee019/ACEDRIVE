// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// What one finished care-package run settled (PORTPLAN item 21): the kits the
// community centre just took, the kits still waiting and the doors they were
// picked up at. The receipt page prints it; the flow banks the numbers.
namespace AceDrive.Core;

/// <summary>
/// One delivered care-package run. <paramref name="Delivered"/> is the money-
/// free total the community centre took, <paramref name="Remaining"/> the kits
/// left for a later run (Unity keeps leftover bags for the next delivery), and
/// <paramref name="Stores"/> the store names the run picked up at, in drive
/// order. A resumed run (quitting after a pickup) has no store list: the route
/// is runtime-only, exactly like Unity's, so the receipt omits the line.
/// </summary>
public sealed record CarePackageReceipt(
    int Delivered,
    int Remaining,
    IReadOnlyList<string>? Stores = null);
