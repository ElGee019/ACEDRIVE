using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Operating-zone world standings (PORTPLAN item 58, Unity
/// ZoneStandingsService). Backend parity: the board is loaded and ranked
/// here; no ACEDRIVE screen consumes the result yet.
/// </summary>
public sealed class FirestoreZoneStandings
{
    private readonly FirestorePlayers _players;
    private readonly Func<string?> _uid;
    private readonly ILog _log;
    private readonly CancellationToken _stopToken;
    private double[] _otherKm = [];
    private ZoneStandingsResult? _last;
    private double _lastZoneKm;
    private int _boardLoaded;
    private int _hasValue;
    private int _loading;

    public FirestoreZoneStandings(
        FirestorePlayers players, Func<string?> uid,
        ILog log, CancellationToken stopToken)
    {
        _players = players;
        _uid = uid;
        _log = log;
        _stopToken = stopToken;
    }

    public ZoneStandingsResult? LastResult => Volatile.Read(ref _last);

    public void Start() => EnsureBoardLoad();

    public ZoneStandingsResult? Refresh(double zoneKm)
    {
        Volatile.Write(ref _lastZoneKm, zoneKm);
        Volatile.Write(ref _hasValue, 1);

        if (Volatile.Read(ref _boardLoaded) == 0)
        {
            EnsureBoardLoad();
            return ZoneStandings.Refresh(
                zoneKm, otherKm: null, boardLoaded: false, last: LastResult);
        }

        ZoneStandingsResult? result = ZoneStandings.Refresh(
            zoneKm, Volatile.Read(ref _otherKm), boardLoaded: true, last: LastResult);
        Publish(result);
        return result;
    }

    public void Push(double zoneKm, string displayName)
    {
        if (_uid() is not { Length: > 0 } uid) return;
        SafeTask.Forget(
            _players.SetZoneKmAsync(uid, zoneKm, displayName, _stopToken),
            _log, "zone km push");
    }

    private void EnsureBoardLoad()
    {
        if (Volatile.Read(ref _boardLoaded) == 1) return;
        if (Interlocked.CompareExchange(ref _loading, 1, 0) != 0) return;
        SafeTask.Forget(LoadBoardAsync(), _log, "zone board");
    }

    private async Task LoadBoardAsync()
    {
        try
        {
            IReadOnlyList<FirestoreDocument>? documents =
                await _players.LoadAsync(_stopToken).ConfigureAwait(false);
            if (documents is null)
            {
                _log.LogWarning("firebase: zone board unavailable");
                return;
            }

            double[] others = [.. PlayerBoard.OtherZoneKm(documents, _uid())];
            Volatile.Write(ref _otherKm, others);
            Volatile.Write(ref _boardLoaded, 1);
            _log.LogInfo($"firebase: zone board ready ({others.Length} other players)");

            if (Volatile.Read(ref _hasValue) == 1)
            {
                Publish(ZoneStandings.Refresh(
                    Volatile.Read(ref _lastZoneKm), others,
                    boardLoaded: true, last: LastResult));
            }
        }
        finally
        {
            Interlocked.Exchange(ref _loading, 0);
        }
    }

    private void Publish(ZoneStandingsResult? result)
    {
        if (result is null) return;
        Volatile.Write(ref _last, result);
    }
}
