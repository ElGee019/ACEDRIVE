namespace AceDrive.Overlay.Services;

/// <summary>
/// The dynamic car-rarity source used by the XP formula. Reads are
/// synchronous against the last loaded board (the receipt must not block on
/// the network); <see cref="RecordKm"/> is a fire-and-forget aggregate write.
/// </summary>
public interface ICarRaritySource
{
    /// <summary>Cars on the global board; 0 while offline.</summary>
    int CarCount { get; }

    /// <summary>Unity CarRarityService.GetRarity: rank, 1 = most driven.</summary>
    int GetRarity(string? carKey);

    /// <summary>All-players km for the car; 0 when unknown.</summary>
    int GetKm(string? carKey);

    /// <summary>Adds one completed owned-car trip to the global km board.</summary>
    void RecordKm(string carKey, int km);
}
