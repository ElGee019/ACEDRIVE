// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 35 and
// LATESTDRIVEBS2 CarRarityService.GetRarity/LoadAll.
namespace AceDrive.Core;

/// <summary>
/// The all-players car-usage board: every car ranked by the kilometres the
/// whole player base drove it, most-driven first. Rarity 1 is therefore the
/// most common car and the last rank the rarest one. Immutable so the
/// background Firestore load can swap a whole table while the UI reads it.
/// </summary>
public sealed class CarRarityTable
{
    private readonly IReadOnlyDictionary<string, int> _rankByKey;
    private readonly IReadOnlyDictionary<string, int> _kmByKey;

    private CarRarityTable(
        IReadOnlyDictionary<string, int> rankByKey,
        IReadOnlyDictionary<string, int> kmByKey,
        int carCount)
    {
        _rankByKey = rankByKey;
        _kmByKey = kmByKey;
        CarCount = carCount;
    }

    /// <summary>The offline fallback: no board, every car answers rarity 1.</summary>
    public static CarRarityTable Empty { get; } = new(
        new Dictionary<string, int>(StringComparer.Ordinal),
        new Dictionary<string, int>(StringComparer.Ordinal),
        0);

    /// <summary>Cars on the board; the unknown-car fallback is this + 1.</summary>
    public int CarCount { get; }

    /// <summary>
    /// Builds the board from raw doc rows. Ties share a rank (dense ranking,
    /// Unity's <c>entries[i].km &lt; lastKm</c> rule), and a key that appears
    /// twice keeps its first row after the sort so a malformed board cannot
    /// throw.
    /// </summary>
    public static CarRarityTable From(IReadOnlyList<CarKm> entries)
    {
        List<CarKm> rows = [];
        foreach (CarKm entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Km < 0)
            {
                continue;
            }
            rows.Add(entry);
        }

        rows.Sort(static (a, b) =>
        {
            int byKm = b.Km.CompareTo(a.Km);
            return byKm != 0 ? byKm : string.CompareOrdinal(a.Key, b.Key);
        });

        Dictionary<string, int> ranks = new(rows.Count, StringComparer.Ordinal);
        Dictionary<string, int> kms = new(rows.Count, StringComparer.Ordinal);
        int rank = 0;
        int lastKm = int.MinValue;
        for (int i = 0; i < rows.Count; i++)
        {
            CarKm row = rows[i];
            if (i == 0 || row.Km < lastKm)
            {
                rank++;
                lastKm = row.Km;
            }
            ranks.TryAdd(row.Key, rank);
            kms.TryAdd(row.Key, row.Km);
        }

        return new CarRarityTable(ranks, kms, ranks.Count);
    }

    /// <summary>All-players km for the car; 0 when the board does not know it.</summary>
    public int Km(string? carKey)
    {
        if (string.IsNullOrWhiteSpace(carKey))
        {
            return 0;
        }
        return _kmByKey.TryGetValue(carKey, out int km) ? km : 0;
    }

    /// <summary>
    /// Unity CarRarityService.GetRarity: a known car answers its rank, an
    /// empty key answers 1 (most common) and a car the board has not seen
    /// yet answers the last rank (rarest) — it has never been driven.
    /// </summary>
    public int Rarity(string? carKey)
    {
        if (string.IsNullOrWhiteSpace(carKey))
        {
            return 1;
        }
        if (_rankByKey.TryGetValue(carKey, out int rank))
        {
            return rank;
        }
        return CarCount == 0 ? 1 : CarCount + 1;
    }
}
