using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Best-single-shift world standings (PORTPLAN item 12, Unity
/// ShiftStandingsService). Backend parity only: no shift-ended screen
/// consumes it yet, but the board math and push are ready.
/// </summary>
public sealed class FirestoreShiftStandings
{
    private readonly FirestorePlayers _players;
    private readonly Func<string?> _uid;
    private readonly ILog _log;
    private readonly CancellationToken _stopToken;
    private ShiftBoardEntry[] _otherEntries = [];
    private ShiftStandingsResult? _last;
    private double _lastEarnings;
    private long _lastFirstEarnedTicks;
    private int _boardLoaded;
    private int _hasValue;
    private int _loading;

    public FirestoreShiftStandings(
        FirestorePlayers players, Func<string?> uid,
        ILog log, CancellationToken stopToken)
    {
        _players = players;
        _uid = uid;
        _log = log;
        _stopToken = stopToken;
    }

    public ShiftStandingsResult? LastResult => Volatile.Read(ref _last);

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

        ShiftStandingsResult? result = ShiftStandings.Refresh(
            earnings, firstEarnedTicks, Volatile.Read(ref _otherEntries),
            boardLoaded: true, last: LastResult);
        Publish(result);
        return result;
    }

    public void Push(double earnings, long firstEarnedTicks, string displayName)
    {
        if (_uid() is not { Length: > 0 } uid || earnings <= 0.0) return;
        SafeTask.Forget(
            _players.SetShiftEarningsAsync(
                uid, earnings, firstEarnedTicks, displayName, _stopToken),
            _log, "shift earnings push");
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
                await _players.LoadAsync(_stopToken).ConfigureAwait(false);
            if (documents is null)
            {
                _log.LogWarning("firebase: shift board unavailable");
                return;
            }

            ShiftBoardEntry[] others =
                [.. PlayerBoard.OtherShiftEntries(documents, _uid())];
            Volatile.Write(ref _otherEntries, others);
            Volatile.Write(ref _boardLoaded, 1);
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

    private void Publish(ShiftStandingsResult? result)
    {
        if (result is null) return;
        Volatile.Write(ref _last, result);
    }
}
