// Road-test pick rotation (PORTPLAN item 49, port of RoadTestService's
// pick/tested bookkeeping; the drive flow itself is a later slice). The pick
// is sticky: it only moves when the road test is completed, never on a week
// roll or an overlay restart — RoadTestSnapshot carries it between sessions
// so the button keeps naming the same car until it has been driven.
// Draws without replacement: a car cannot repeat until every car ran, then
// the tested set resets. Randomness is a seeded instance (AGENTS.md), never
// the shared global, so tests pin the sequence while the app root seeds a
// fresh draw per launch. State transitions are allocation-free except on
// rotation, which runs on a manual tap — never in the hot poll.
namespace AceDrive.Core;

public sealed class RoadTestLedger
{
    private readonly IReadOnlyList<CarSpec> _pool;
    private readonly Random _rng;
    private readonly HashSet<string> _tested = new(StringComparer.Ordinal);

    public RoadTestLedger(IReadOnlyList<CarSpec> pool, int seed, RoadTestSnapshot? saved = null)
    {
        _pool = pool;
        _rng = new Random(seed);
        Resume(saved);
        if (Current is null)
        {
            PickNext();
        }
    }

    /// <summary>The car offered now; null when the pool is empty.</summary>
    public CarSpec? Current { get; private set; }

    /// <summary>Road-test keys banked since the last reset, oldest first.</summary>
    public IReadOnlyCollection<string> TestedIds => _tested;

    /// <summary>
    /// The current car was driven: bank it and switch the display to the
    /// next random pick immediately. This is the only road-test change the
    /// game offers — a new week or a restart keeps the car. No-op without a
    /// pool.
    /// </summary>
    public void MarkTested()
    {
        BankCurrent();
        PickNext();
    }

    /// <summary>
    /// What a restart must reopen: this car and the bank. Cold path only
    /// (session start and every completion), so the copy is free of the
    /// 10 Hz loop.
    /// </summary>
    public RoadTestSnapshot Snapshot() => new(Current?.Key, [.. _tested]);

    // Reopens last session's pick. Keys the current cars.json no longer holds
    // drop out, so a stale bank cannot hide live cars and a vanished pick
    // falls through to a fresh draw. A saved folder id (pre-variant file)
    // still resolves its null-preset row through CarSpec.Key.
    private void Resume(RoadTestSnapshot? saved)
    {
        if (saved is null)
        {
            return;
        }
        foreach (string id in saved.TestedIds)
        {
            if (FindKey(id) is not null)
            {
                _tested.Add(id);
            }
        }
        Current = FindKey(saved.CarId);
    }

    private CarSpec? FindKey(string? key)
    {
        if (key is null)
        {
            return null;
        }
        foreach (CarSpec car in _pool)
        {
            if (string.Equals(car.Key, key, StringComparison.Ordinal))
            {
                return car;
            }
        }
        return null;
    }

    private void BankCurrent()
    {
        if (Current is not null)
        {
            _tested.Add(Current.Key);
        }
    }

    private void PickNext()
    {
        Current = null;
        if (_pool.Count == 0)
        {
            return;
        }
        List<CarSpec> fresh = [];
        foreach (CarSpec car in _pool)
        {
            if (!_tested.Contains(car.Key))
            {
                fresh.Add(car);
            }
        }
        if (fresh.Count == 0)
        {
            _tested.Clear();
            fresh.AddRange(_pool);
        }
        Current = fresh[_rng.Next(fresh.Count)];
    }
}
