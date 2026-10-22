using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>In-memory rarity board for XP/receipt flow tests.</summary>
public sealed class FakeCarRarity : ICarRaritySource
{
    public Dictionary<string, int> RarityByKey { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> KmByKey { get; } = new(StringComparer.Ordinal);
    public List<(string Key, int Km)> Recorded { get; } = [];
    public int CarCount { get; set; }

    public int GetRarity(string? carKey) =>
        carKey is not null && RarityByKey.TryGetValue(carKey, out int rank) ? rank : 1;

    public int GetKm(string? carKey) =>
        carKey is not null && KmByKey.TryGetValue(carKey, out int km) ? km : 0;

    public void RecordKm(string carKey, int km) => Recorded.Add((carKey, km));
}
