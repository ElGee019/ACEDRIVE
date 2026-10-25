// Cento Miglia page inputs (PORTPLAN item 49): the calendar stamp the header
// prints and the driver readings the tier gates and eligibility lines show.
// Gathered by the caller so the page formats and gates but never fetches.
namespace AceDrive.Core;

/// <summary>
/// Everything the Cento Miglia page needs besides its tier table. Unity's
/// screen reads the same six values straight off PlayerData and CalendarManager;
/// the port hands them over as one record so the page stays a formatter and the
/// tests can pin a fresh driver in one line.
/// </summary>
public sealed record CentoMigliaFacts(
    int Week,
    int Year,
    double ClubReputation,
    bool HasDiscoveredStreets,
    int CarsOwned,
    int CarClass);
