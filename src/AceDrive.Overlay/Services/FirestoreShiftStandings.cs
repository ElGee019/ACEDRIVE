using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Best-single-shift world standings (PORTPLAN item 12, Unity
/// ShiftStandingsService): the board is read once from <c>acevo_players</c>
/// and cached, and <see cref="Refresh"/> stays synchronous so the
/// shift-ended page can render immediately while the network load and the
/// player's own push run in the background. Only the CURRENT username row is
/// excluded, so an earlier username of the same player stays a rival.
/// </summary>
public sealed class FirestoreShiftStandings : IShiftStandings
{
    private readonly FirestorePlayers _players;
    private readonly Func<string?> _uid;
    private readonly Func<string?> _ownName;
    private readonly ILog _log;
    private readonly CancellationToken _stopToken;
    private IReadOnlyList<FirestoreDocument>? _documents;
    private ShiftStandingsResult? _last;
    private double _lastEarnings;
    private long _lastFirstEarnedTicks;
    private int _boardLoaded;
    private int _hasValue;
    private int _loading;

    public FirestoreShiftStandings(
        FirestorePlayers players, Func<string?> uid, Func<string?> ownName,
        ILog log, CancellationToken stopToken)
    {
        _players = players;
        _uid = uid;
        _ownName = ownName;
        _log = log;
        _stopToken = stopToken;
    }

    public ShiftStandingsResult? LastResult => Volatile.Read(ref _last);

    public event Action<ShiftStandingsResult>? ResultUpdated;

    public void RestoreSnapshot(DriverState? saved)
    {
        if (saved is null || saved.ShiftRank <= 0 || saved.ShiftTotal <= 0)
        {
            return;
        }
        Volatile.Write(ref _last, new ShiftStandingsResult(
            saved.ShiftRank, saved.ShiftTotal, 0, saved.ShiftRank, false));
    }

    /// <summary>Starts the boot-time board load without blocking startup.</summary>
    public void Start() => EnsureBoardLoad();

    public ShiftStandingsResult? Refresh(double earnings, long firstEarnedTicks)
    {
        Volatile.Write(ref _lastEarnings, earnings);
        Volatile.Write(ref _lastFirstEarnedTicks, firstEarnedTicks);
        Volatile.Write(ref _hasValue, 1);

        if (Volatile.Read(ref _boardLoaded) == 0)
        {
            EnsureBoardLoad();
            return ShiftStandings.Refresh(
                earnings, firstEarnedTicks, otherEntries: null,
                boardLoaded: false, last: LastResult);
        }

        IReadOnlyList<FirestoreDocument>? documents = Volatile.Read(ref _documents);
        if (documents is null)
        {
            return LastResult;
        }

        ShiftStandingsResult? result = ShiftStandings.Refresh(
            earnings, firstEarnedTicks, OtherEntries(documents),
            boardLoaded: true, last: LastResult);
        Publish(result);
        return result;
    }

    private void EnsureBoardLoad()
    {
        if (Volatile.Read(ref _boardLoaded) == 1) return;
        if (Interlocked.CompareExchange(ref _loading, 1, 0) != 0) return;
        SafeTask.Forget(LoadBoardAsync(), _log, "shift board");
    }

    private async Task LoadBoardAsync()
    {
        try
        {
            IReadOnlyList<FirestoreDocument>? documents =
                await _players.LoadAsync(_uid, _stopToken).ConfigureAwait(false);
            if (documents is null)
            {
                _log.LogWarning("firebase: shift board unavailable");
                return;
            }

            Volatile.Write(ref _documents, documents);
            Volatile.Write(ref _boardLoaded, 1);
            ShiftBoardEntry[] others = OtherEntries(documents);
            _log.LogInfo($"firebase: shift board ready ({others.Length} other players)");

            if (Volatile.Read(ref _hasValue) == 1)
            {
                Publish(ShiftStandings.Refresh(
                    Volatile.Read(ref _lastEarnings),
                    Volatile.Read(ref _lastFirstEarnedTicks),
                    others, boardLoaded: true, last: LastResult));
            }
        }
        finally
        {
            Interlocked.Exchange(ref _loading, 0);
        }
    }

    // Recomputed per refresh so the name the opening screen confirms later
    // stops being counted as a rival in the same session.
    private ShiftBoardEntry[] OtherEntries(IReadOnlyList<FirestoreDocument> documents) =>
        [.. PlayerBoard.OtherShiftEntries(documents, _uid(), _ownName())];

    private void Publish(ShiftStandingsResult? result)
    {
        if (result is null) return;
        Volatile.Write(ref _last, result);
        ResultUpdated?.Invoke(result);
    }
}
