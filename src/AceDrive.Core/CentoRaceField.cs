// Cento Miglia field draw (PORTPLAN item 49, port of Unity
// RaceParticipantPicker.Pick + MilleMigliaManager's placement table). Unity
// simulates its three NPCs around the route and interleaves the player on
// arrival; the port does not drive the rivals, so the settle shuffles the four
// entrants into a seeded finishing order instead. Pure logic: the caller owns
// the Random, the page owns the words.
namespace AceDrive.Core;

/// <summary>
/// Draws the race's finishing order: the player plus the club roster, each
/// entrant equally likely to take any place. A seeded <see cref="Random"/> keeps
/// the draw testable, and an empty roster degrades to the solo first place the
/// receipt already knew how to print.
/// </summary>
public static class CentoRaceField
{
    /// <summary>
    /// Shuffles the player and the rivals into one finishing order. The place is
    /// uniform over the field, which is the point: the first-place purse, the
    /// concentration award and the Star of the Show multiplier all key on it, so
    /// a race can no longer always open on the winner's line.
    /// </summary>
    public static CentoRaceResult Draw(
        IReadOnlyList<CentoCompetitor>? rivals,
        string playerName,
        string playerCar,
        int playerRating,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);

        var field = new List<CentoStanding>((rivals?.Count ?? 0) + 1);
        if (rivals is not null)
        {
            foreach (CentoCompetitor rival in rivals)
            {
                if (rival is null)
                {
                    continue;
                }

                field.Add(new CentoStanding(
                    0, rival.Name, rival.Car, rival.DriverRating, IsPlayer: false));
            }
        }

        field.Add(new CentoStanding(
            0, playerName ?? string.Empty, playerCar ?? string.Empty,
            playerRating, IsPlayer: true));

        // Fisher-Yates on the caller's seeded stream: every entrant reaches
        // every place with the same probability, and the same seed replays the
        // same race.
        for (int i = field.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (field[i], field[j]) = (field[j], field[i]);
        }

        int playerPlacement = 1;
        for (int i = 0; i < field.Count; i++)
        {
            field[i] = field[i] with { Place = i + 1 };
            if (field[i].IsPlayer)
            {
                playerPlacement = i + 1;
            }
        }

        return new CentoRaceResult(playerPlacement, field);
    }
}
