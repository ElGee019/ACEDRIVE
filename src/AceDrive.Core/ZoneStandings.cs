// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 58 and
// LATESTDRIVEBS2 ZoneStandingsService.CalculatePlayerRank/Refresh.
namespace AceDrive.Core;

/// <summary>
/// Longest-operating-zone world standings: strictly greater zone km wins,
/// ties never overtake (Unity parity). The board is somebody else's job.
/// </summary>
public static class ZoneStandings
{
    public static int Rank(double zoneKm, IReadOnlyList<double> otherKm) =>
        RivalsStandings.Rank(zoneKm, otherKm);

    /// <summary>A fresh result against the loaded board, or the last one while missing.</summary>
    public static ZoneStandingsResult? Refresh(
        double zoneKm,
        IReadOnlyList<double>? otherKm,
        bool boardLoaded,
        ZoneStandingsResult? last)
    {
        if (!boardLoaded)
        {
            return last;
        }

        int count = otherKm?.Count ?? 0;
        int current = Rank(zoneKm, otherKm ?? []);
        int total = count + 1;
        int previous = last?.CurrentRank ?? total;
        return new ZoneStandingsResult(
            current, total, previous - current, previous, last is null);
    }
}
