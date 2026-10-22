using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Firestore-backed player telemetry. Each call is one merge write on the
/// player's own doc or one country increment; failures never gate gameplay.
/// Calls made before anonymous auth has produced a uid are dropped — the
/// next job/shift/zone event pushes again.
/// </summary>
public sealed class FirestorePlayerTelemetry : IPlayerTelemetry
{
    private readonly FirestorePlayers _players;
    private readonly FirestoreCountries _countries;
    private readonly Func<string?> _uid;
    private readonly ILog _log;
    private readonly CancellationToken _stopToken;

    public FirestorePlayerTelemetry(
        FirestorePlayers players, FirestoreCountries countries,
        Func<string?> uid, ILog log, CancellationToken stopToken)
    {
        _players = players;
        _countries = countries;
        _uid = uid;
        _log = log;
        _stopToken = stopToken;
    }

    public void PushZoneKm(double zoneKm, string displayName) =>
        SafeTask.Forget(PushZoneKmAsync(zoneKm, displayName), _log, "zone km push");

    public void PushShiftEarnings(double earnings, long firstEarnedTicks, string displayName) =>
        SafeTask.Forget(
            PushShiftEarningsAsync(earnings, firstEarnedTicks, displayName),
            _log, "shift earnings push");

    public void IncrementCountryKm(string country, int km) =>
        SafeTask.Forget(
            IncrementCountryKmAsync(country, km), _log, "country km push");

    /// <summary>Awaitable twins for the fire-and-forget interface methods.</summary>
    public Task PushZoneKmAsync(double zoneKm, string displayName) =>
        _uid() is { Length: > 0 } uid
            ? _players.SetZoneKmAsync(uid, zoneKm, displayName, _stopToken)
            : Task.CompletedTask;

    public Task PushShiftEarningsAsync(
        double earnings, long firstEarnedTicks, string displayName) =>
        _uid() is { Length: > 0 } uid && earnings > 0.0
            ? _players.SetShiftEarningsAsync(
                uid, earnings, firstEarnedTicks, displayName, _stopToken)
            : Task.CompletedTask;

    public Task IncrementCountryKmAsync(string country, int km) =>
        _uid() is { Length: > 0 } && km > 0
            ? _countries.IncrementKmAsync(country, km, _stopToken)
            : Task.CompletedTask;
}
