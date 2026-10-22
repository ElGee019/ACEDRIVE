using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Records the online pushes a settle/shift/zone emits.</summary>
public sealed class FakePlayerTelemetry : IPlayerTelemetry
{
    public List<(double Km, string Name)> ZonePushes { get; } = [];
    public List<(double Earnings, long Ticks, string Name)> ShiftPushes { get; } = [];
    public List<(string Country, int Km)> CountryPushes { get; } = [];

    public void PushZoneKm(double zoneKm, string displayName) =>
        ZonePushes.Add((zoneKm, displayName));

    public void PushShiftEarnings(double earnings, long firstEarnedTicks, string displayName) =>
        ShiftPushes.Add((earnings, firstEarnedTicks, displayName));

    public void IncrementCountryKm(string country, int km) =>
        CountryPushes.Add((country, km));
}
