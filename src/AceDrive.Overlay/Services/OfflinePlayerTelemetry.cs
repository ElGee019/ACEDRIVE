namespace AceDrive.Overlay.Services;

/// <summary>Offline telemetry: every push is a silent no-op.</summary>
public sealed class OfflinePlayerTelemetry : IPlayerTelemetry
{
    public void PushZoneKm(double zoneKm, string displayName)
    {
    }

    public void PushShiftEarnings(double earnings, long firstEarnedTicks, string displayName)
    {
    }

    public void IncrementCountryKm(string country, int km)
    {
    }
}
