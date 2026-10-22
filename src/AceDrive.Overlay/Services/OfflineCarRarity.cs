namespace AceDrive.Overlay.Services;

/// <summary>
/// Offline rarity: every car is class 1 × rarity 1 and no km is written.
/// This preserves the pre-online XP balance when no Firebase config exists.
/// </summary>
public sealed class OfflineCarRarity : ICarRaritySource
{
    public int CarCount => 0;
    public int GetRarity(string? carKey) => 1;
    public int GetKm(string? carKey) => 0;
    public void RecordKm(string carKey, int km)
    {
    }
}
