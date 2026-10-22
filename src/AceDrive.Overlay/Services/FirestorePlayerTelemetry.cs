using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Firestore-backed player telemetry. Each call is one merge write on the
/// player's own doc or one country increment; failures never gate gameplay.
/// The uid is resolved by awaiting anonymous auth instead of reading the
/// cached session: the boot poll can end a shift that timed out while the
/// game was closed before the first sign-in has finished, and a shift's
/// record is only pushed at its end — dropping that push would keep a new
/// player (a fresh uid with no doc yet) off the shift board until a later
/// shift ends. XP, zone and country pushes self-heal on their next event;
/// the shift push has no such retry, so it waits for the uid.
/// </summary>
public sealed class FirestorePlayerTelemetry : IPlayerTelemetry
{
    private readonly FirestorePlayers _players;
    private readonly FirestoreCountries _countries;
    private readonly Func<CancellationToken, Task<string?>> _uidAsync;
    private readonly ILog _log;
    private readonly CancellationToken _stopToken;

    public FirestorePlayerTelemetry(
        FirestorePlayers players, FirestoreCountries countries,
        Func<CancellationToken, Task<string?>> uidAsync, ILog log, CancellationToken stopToken)
    {
        _players = players;
        _countries = countries;
        _uidAsync = uidAsync;
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
    public async Task PushZoneKmAsync(double zoneKm, string displayName)
    {
        if (await UidAsync() is not { Length: > 0 } uid) return;
        await _players.SetZoneKmAsync(uid, zoneKm, displayName, _stopToken).ConfigureAwait(false);
    }

    public async Task PushShiftEarningsAsync(
        double earnings, long firstEarnedTicks, string displayName)
    {
        if (earnings <= 0.0) return;
        if (await UidAsync() is not { Length: > 0 } uid) return;
        await _players.SetShiftEarningsAsync(
            uid, earnings, firstEarnedTicks, displayName, _stopToken).ConfigureAwait(false);
    }

    public async Task IncrementCountryKmAsync(string country, int km)
    {
        if (km <= 0) return;
        if (await UidAsync() is not { Length: > 0 }) return;
        await _countries.IncrementKmAsync(country, km, _stopToken).ConfigureAwait(false);
    }

    // Offline or a rejected sign-in answers null, the same silent degrade the
    // cached-uid path had; SafeTask turns a transport exception into a log.
    private async Task<string?> UidAsync() =>
        await _uidAsync(_stopToken).ConfigureAwait(false);
}
