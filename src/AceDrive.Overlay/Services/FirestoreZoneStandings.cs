using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Operating-zone world standings (PORTPLAN item 58, Unity
/// ZoneStandingsService). Backend parity: the board is loaded and ranked
/// here; no ACEDRIVE screen consumes the result yet. Only the CURRENT
/// username row is excluded, so an earlier username of the same player stays
/// a rival.
/// </summary>
public sealed class FirestoreZoneStandings
{
    private readonly FirestorePlayers _players;
    private readonly Func<string?> _uid;
    private readonly Func<string?> _ownName;
    private readonly ILog _log;
    private readonly CancellationToken _stopToken;
    private IReadOnlyList<FirestoreDocument>? _documents;
    private ZoneStandingsResult? _last;
    private double _lastZoneKm;
    private int _boardLoaded;
    private int _hasValue;
    private int _loading;

    public FirestoreZoneStandings(
        FirestorePlayers players, Func<string?> uid, Func<string?> ownName,
        ILog log, CancellationToken stopToken)
    {
        _players = players;
        _uid = uid;
        _ownName = ownName;
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

        IReadOnlyList<FirestoreDocument>? documents = Volatile.Read(ref _documents);
        if (documents is null)
        {
            return LastResult;
        }

        ZoneStandingsResult? result = ZoneStandings.Refresh(
            zoneKm, OtherKm(documents), boardLoaded: true, last: LastResult);
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
                await _players.LoadAsync(_uid, _stopToken).ConfigureAwait(false);
            if (documents is null)
            {
                _log.LogWarning("firebase: zone board unavailable");
                return;
            }

            Volatile.Write(ref _documents, documents);
            Volatile.Write(ref _boardLoaded, 1);
            double[] others = OtherKm(documents);
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

    // Recomputed per refresh so the name the opening screen confirms later
    // stops being counted as a rival in the same session.
    private double[] OtherKm(IReadOnlyList<FirestoreDocument> documents) =>
        [.. PlayerBoard.OtherZoneKm(documents, _uid(), _ownName())];

    private void Publish(ZoneStandingsResult? result)
    {
        if (result is null) return;
        Volatile.Write(ref _last, result);
    }
}
