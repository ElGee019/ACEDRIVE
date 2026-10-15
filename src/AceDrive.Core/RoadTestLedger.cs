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
    private readonly IReadOnlyList<RoadTestCar> _pool;
    private readonly Random _rng;
    private readonly HashSet<string> _tested = new(StringComparer.Ordinal);

    public RoadTestLedger(IReadOnlyList<RoadTestCar> pool, int seed, RoadTestSnapshot? saved = null)
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
    public RoadTestCar? Current { get; private set; }

    /// <summary>Ids banked since the last reset, oldest first.</summary>
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
    public RoadTestSnapshot Snapshot() => new(Current?.Id, [.. _tested]);

    // Reopens last session's pick. Ids the current cars.json no longer holds
    // drop out, so a stale bank cannot hide live cars and a vanished pick
    // falls through to a fresh draw.
    private void Resume(RoadTestSnapshot? saved)
    {
        if (saved is null)
        {
            return;
        }
        foreach (string id in saved.TestedIds)
        {
            if (Find(id) is not null)
            {
                _tested.Add(id);
            }
        }
        Current = Find(saved.CarId);
    }

    private RoadTestCar? Find(string? id)
    {
        if (id is null)
        {
            return null;
        }
        foreach (RoadTestCar car in _pool)
        {
            if (string.Equals(car.Id, id, StringComparison.Ordinal))
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
            _tested.Add(Current.Id);
        }
    }

    private void PickNext()
    {
        Current = null;
        if (_pool.Count == 0)
        {
            return;
        }
        List<RoadTestCar> fresh = [];
        foreach (RoadTestCar car in _pool)
        {
            if (!_tested.Contains(car.Id))
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
