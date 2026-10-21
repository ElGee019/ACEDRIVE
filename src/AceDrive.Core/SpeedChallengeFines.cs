// Speed-challenge fine check (PORTPLAN item 11, Phase C). Pure and guarded for
// the 10 Hz poll: invalid telemetry answers no fine instead of throwing.
namespace AceDrive.Core;

/// <summary>
/// The actual speed gamble: an agreed challenge is fined once per patrol when
/// the car passes inside that patrol's radius above its posted limit. Exactly
/// at the limit stays legal, and a patrol whose id is already in the fined set
/// cannot charge twice.
/// </summary>
public static class SpeedChallengeFines
{
    /// <summary>
    /// The patrol that caught the car this tick, or null when the challenge
    /// was declined, the car is outside every radius, is not speeding, the
    /// position is unusable, or every patrol in reach was already fined.
    /// </summary>
    public static PolicePatrol? Check(
        IReadOnlyList<PolicePatrol> patrols,
        double normalizedPosition,
        double trackLengthMeters,
        double speedKmh,
        IReadOnlySet<int> fined,
        bool agreed)
    {
        if (!agreed
            || patrols is not { Count: > 0 }
            || !double.IsFinite(speedKmh))
        {
            return null;
        }

        double offset = LapGeometry.OffsetMeters(normalizedPosition, trackLengthMeters);
        if (!double.IsFinite(offset))
        {
            return null;
        }

        foreach (PolicePatrol patrol in patrols)
        {
            if (fined.Contains(patrol.Id)
                || speedKmh <= patrol.SpeedLimitKmh
                || !double.IsFinite(patrol.RadiusMeters)
                || patrol.RadiusMeters < 0.0)
            {
                continue;
            }

            double distance = LapGeometry.DistanceMeters(
                offset, patrol.LapOffsetMeters, trackLengthMeters);
            if (distance <= patrol.RadiusMeters)
            {
                return patrol;
            }
        }
        return null;
    }
}
