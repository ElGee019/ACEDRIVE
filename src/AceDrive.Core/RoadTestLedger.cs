// Road-test pick rotation and settled record (PORTPLAN item 49, port of
// RoadTestService's pick/tested bookkeeping plus PlayerData's lastRoadTest* /
// highestRoadTest* readings). The pick is sticky: it only moves on MarkTested,
// never on a week roll or an overlay restart — RoadTestSnapshot carries it
// between sessions so the button keeps naming the same car until it has been
// driven. The receipt record travels in the same snapshot: both halves belong
// to the one weekly event.
// Draws without replacement: a car cannot repeat until every car ran, then
// the tested set resets. Randomness is a seeded instance (AGENTS.md), never
// the shared global, so tests pin the sequence while the app root seeds a
// fresh draw per launch. State transitions are allocation-free except on
// rotation, which the completion path runs — never in the hot poll.
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
    /// The settled readings a restart reopens: the natural car-journalist
    /// reputation, the last shoot and the previous issue's numbers with their
    /// bests. Empty until the first receipt.
    /// </summary>
    public RoadTestRecord Record { get; private set; } = RoadTestRecord.Empty;

    /// <summary>
    /// The current car was fully tested (Unity RoadTestService
    /// MarkCurrentAsTested): the car is banked, the next random pick becomes the
    /// display, and the natural car-journalist reputation rises by one point
    /// (capped), which is what the receipt's formula scales. This is the only
    /// road-test change the game offers — a new week or a restart keeps the car.
    /// No-op rotation without a pool; the reputation still counts, because the
    /// test was driven.
    /// </summary>
    public void MarkTested()
    {
        BankCurrent();
        PickNext();
        Record = Record with
        {
            NaturalReputation =
                RoadTestAudience.NextNaturalReputation(Record.NaturalReputation),
        };
    }

    /// <summary>
    /// Banks a settled receipt: the new readings become the next issue's
    /// "previously …" baseline and its highs become the bests. Called once per
    /// receipt, after <see cref="MarkTested"/> raised the natural reputation the
    /// formula used.
    /// </summary>
    public void RecordReceipt(RoadTestReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        Record = receipt.ToRecord();
    }

    /// <summary>
    /// What a restart must reopen: this car, the bank and the settled record.
    /// Cold path only (session start and every completion), so the copies are
    /// free of the 10 Hz loop.
    /// </summary>
    public RoadTestSnapshot Snapshot() => new(Current?.Key, [.. _tested], Record);

    // Reopens last session's pick and record. Keys the current cars.json no
    // longer holds drop out, so a stale bank cannot hide live cars and a
    // vanished pick falls through to a fresh draw. A saved folder id
    // (pre-variant file) still resolves its null-preset row through CarSpec.Key.
    private void Resume(RoadTestSnapshot? saved)
    {
        if (saved is null)
        {
            return;
        }
        if (saved.Record is { } record)
        {
            Record = record;
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
