// Cento Miglia competitor roster (PORTPLAN item 49, port of Unity
// MilleMigliaParticipant): one club rival the settle shuffles into the field.
// Pure data, no driving: the port does not simulate the NPCs, it only draws the
// finishing order the receipt prints.
namespace AceDrive.Core;

/// <summary>
/// One competitor the Cento Miglia draws against the player. Unity stores the
/// same pair on MilleMigliaParticipant (a resident's name and a catalogue car)
/// plus a transient per-race driver rating; the port keeps the rating on the
/// roster because it is printed on the final standings and nothing simulates
/// it.
/// </summary>
/// <param name="Name">The rival's display name.</param>
/// <param name="Car">The car the rival is credited with, a display name.</param>
/// <param name="DriverRating">The rival's overall driver rating, shown on the
/// standings row (Unity's rounded Overall).</param>
public sealed record CentoCompetitor(
    string Name,
    string Car,
    int DriverRating);
