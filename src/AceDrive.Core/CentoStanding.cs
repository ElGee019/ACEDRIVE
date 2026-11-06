// Cento Miglia final standings (PORTPLAN item 49, port of Unity
// CentoMigliaScreen.BuildFinalStandingsText): one row of the receipt's RESULTS
// block. Pure data; the page formats the row and marks the player's line.
namespace AceDrive.Core;

/// <summary>
/// One finisher in the race's final standings. Unity builds these rows from the
/// NPC runtimes plus the player and sorts them by place; the port's field is
/// drawn, never driven, so the same row shape carries the drawn order.
/// </summary>
/// <param name="Place">The finishing place, 1 for the winner.</param>
/// <param name="Name">The competitor's display name; empty when the port has no
/// player name, which the page prints as its localised YOU.</param>
/// <param name="Car">The car the competitor ran, a display name.</param>
/// <param name="DriverRating">The competitor's overall driver rating.</param>
/// <param name="IsPlayer">True on the player's own row, which the receipt
/// highlights.</param>
public sealed record CentoStanding(
    int Place,
    string Name,
    string Car,
    int DriverRating,
    bool IsPlayer);
