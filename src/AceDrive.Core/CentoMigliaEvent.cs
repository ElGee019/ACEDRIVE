// Cento Miglia entry (PORTPLAN item 49): what the page built when the player
// pressed ENTER EVENT. Pure data, so the race port can take it without
// reaching back into the page.
namespace AceDrive.Core;

/// <summary>
/// One built Cento Miglia event: the club that issued it, the calendar stamp it
/// was entered under, the registration number its tier drew and the route the
/// operating zone allowed. Unity keeps the same facts on MilleMigliaManager
/// (tier, eventYear/eventWeek, regNumber and the route addresses); the port
/// hands them to the race as one record.
/// </summary>
public sealed record CentoMigliaEvent(
    int TierNumber,
    string TierLabel,
    int Registration,
    int Year,
    int Week,
    CentoRoutePlan Route);
