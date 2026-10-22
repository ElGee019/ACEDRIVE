namespace AceDrive.Overlay.Services;

/// <summary>
/// Fire-and-forget world-board writes for player progress that is not XP
/// (XP has its own <see cref="IRivalsService"/> seam). Small on purpose:
/// zone, best shift and country km, exactly the Unity pushes.
/// </summary>
public interface IPlayerTelemetry
{
    void PushZoneKm(double zoneKm, string displayName);
    void PushShiftEarnings(double earnings, long firstEarnedTicks, string displayName);
    void IncrementCountryKm(string country, int km);
}
