// Cento Miglia drawn result (PORTPLAN item 49): the player's finishing place
// and the field's final standings, produced together so the receipt can never
// print a place the table does not agree with.
namespace AceDrive.Core;

/// <summary>
/// The field draw's answer: where the player finished and the full finishing
/// order. The settle prices the place, the Star of the Show roll and the
/// receipt's RESULTS block from this one record.
/// </summary>
/// <param name="PlayerPlacement">The player's finishing place (1 to the field
/// size; 1 when the roster is empty).</param>
/// <param name="Standings">Every finisher in place order, the player's row
/// flagged so the receipt can highlight it.</param>
public sealed record CentoRaceResult(
    int PlayerPlacement,
    IReadOnlyList<CentoStanding> Standings);
