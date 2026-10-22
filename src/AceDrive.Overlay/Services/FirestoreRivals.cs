using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Live XP world standings (PORTPLAN item 60, Unity RivalsService). The
/// board is read once from <c>acevo_players</c> and cached; <see cref="Refresh"/>
/// stays synchronous so the receipt can render immediately, while the network
/// load and the XP push run in the background. Own doc is excluded from the
/// board, exactly like Unity's CollectOtherXp.
/// </summary>
public sealed class FirestoreRivals : IRivalsService
{
    private readonly FirestorePlayers _players;
    private readonly Func<string?> _uid;
    private readonly ILog _log;
    private readonly CancellationToken _stopToken;
    private double[] _otherXp = [];
    private RivalsResult? _last;
    private double _lastPlayerXp;
    private int _boardLoaded;
    private int _hasPlayerXp;
    private int _loading;
    private Task? _loadTask;

    public FirestoreRivals(
        FirestorePlayers players, Func<string?> uid,
        ILog log, CancellationToken stopToken)
    {
        _players = players;
        _uid = uid;
        _log = log;
        _stopToken = stopToken;
    }

    public RivalsResult? LastResult => Volatile.Read(ref _last);

    public event Action<RivalsResult>? ResultUpdated;

    public void RestoreSnapshot(DriverState? saved)
    {
        if (saved is null || saved.RivalsRank <= 0 || saved.RivalsTotal <= 0)
        {
            return;
        }
        Volatile.Write(ref _last, new RivalsResult(
            saved.RivalsRank, saved.RivalsTotal, 0, saved.RivalsRank, false));
    }

    /// <summary>Starts the boot-time board load without blocking startup.</summary>
    public void Start() => EnsureBoardLoad();

    /// <summary>The current board load, for a cold-path caller/tests to await.</summary>
    public Task RefreshBoardAsync()
    {
        EnsureBoardLoad();
        return _loadTask ?? Task.CompletedTask;
    }

    public RivalsResult? Refresh(double playerXp)
    {
        Volatile.Write(ref _lastPlayerXp, playerXp);
        Volatile.Write(ref _hasPlayerXp, 1);

        RivalsResult? last = LastResult;
        if (Volatile.Read(ref _boardLoaded) == 0)
        {
            // Capture the restored snapshot before starting the load: a fast
            // synchronous board would otherwise replace LastResult mid-call
            // and hide Unity's fallback answer.
            EnsureBoardLoad();
            return RivalsStandings.Refresh(
                playerXp, otherXp: null, boardLoaded: false, last: last);
        }

        RivalsResult? result = RivalsStandings.Refresh(
            playerXp, Volatile.Read(ref _otherXp), boardLoaded: true, last: last);
        Publish(result);
        return result;
    }

    public void PushXp(double xp, string displayName) =>
        SafeTask.Forget(PushXpAsync(xp, displayName), _log, "rivals xp push");

    /// <summary>The write behind <see cref="PushXp"/>, awaitable on cold paths.</summary>
    public Task PushXpAsync(double xp, string displayName) =>
        _uid() is { Length: > 0 } uid
            ? _players.SetXpAsync(uid, xp, displayName, _stopToken)
            : Task.CompletedTask;

    private void EnsureBoardLoad()
    {
        if (Volatile.Read(ref _boardLoaded) == 1) return;
        if (Interlocked.CompareExchange(ref _loading, 1, 0) != 0) return;
        _loadTask = LoadBoardAsync();
        SafeTask.Forget(_loadTask, _log, "rivals board");
    }

    private async Task LoadBoardAsync()
    {
        try
        {
            IReadOnlyList<FirestoreDocument>? documents =
                await _players.LoadAsync(_stopToken).ConfigureAwait(false);
            if (documents is null)
            {
                _log.LogWarning("firebase: XP board unavailable");
                return;
            }

            double[] others = [.. PlayerBoard.OtherXp(documents, _uid())];
            Volatile.Write(ref _otherXp, others);
            Volatile.Write(ref _boardLoaded, 1);
            _log.LogInfo($"firebase: XP board ready ({others.Length} other players)");

            if (Volatile.Read(ref _hasPlayerXp) == 1)
            {
                Publish(RivalsStandings.Refresh(
                    Volatile.Read(ref _lastPlayerXp), others,
                    boardLoaded: true, last: LastResult));
            }
        }
        finally
        {
            Interlocked.Exchange(ref _loading, 0);
        }
    }

    private void Publish(RivalsResult? result)
    {
        if (result is null) return;
        Volatile.Write(ref _last, result);
        ResultUpdated?.Invoke(result);
    }
}
