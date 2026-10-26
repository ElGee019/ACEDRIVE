// Cento Miglia route plan (PORTPLAN item 49, port of Unity MilleMigliaService's
// route result): the two doors the race runs between and the distance the
// address map puts between them. Pure data, so the page, the companion and the
// settle all read the same plan.
namespace AceDrive.Core;

/// <summary>
/// One planned Cento Miglia route: the start door, the finish door and the
/// closed-lap distance between them in kilometres. The plan is the event's
/// route (Unity's MilleMigliaRoute.startAddress/endAddress/distanceKm), so a
/// later NPC field or a receipt can read it without re-deriving the addresses.
/// </summary>
public sealed record CentoRoutePlan(
    AddressLocation Start,
    AddressLocation Finish,
    double DistanceKm);
