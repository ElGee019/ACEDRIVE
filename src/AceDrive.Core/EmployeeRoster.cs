// The community centre's hiring rules (PORTPLAN item 54, port of Unity
// CommunityCentre.OnHireEmployeeClicked and EmployeeService's hire draws).
// Pure state: the store owns the file, the page paints the roster, and this
// class decides who may be hired and when a fresh roster is rolled.
namespace AceDrive.Core;

/// <summary>
/// The crew the player hired plus the candidates the community centre offers
/// this week. Unity rolls five residents every new week, refills a hole after
/// a hire, and never offers somebody already on the crew; this class keeps the
/// same rules. Names are the resident table's owner names, because that is
/// also the friendship key a hire warms.
///
/// The roster is cached in memory and written only when it changes: the first
/// hire-page use pays one guarded read, a hire saves immediately (Unity's
/// SaveManager.Save on the row's click), and a weekly reroll is the other
/// write. Cold path only — the page opens on a POI arrival, never the poll.
/// </summary>
public sealed class EmployeeRoster
{
    /// <summary>Unity CommunityCentre.HireCandidateCount: five rows a week.</summary>
    public const int CandidateCount = 5;

    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IEmployeeStore _store;
    private readonly Random _rng;
    private List<string> _employees = [];
    private List<string> _candidates = [];
    private int _rosterWeek = -1;
    // WHY lazy: the composition root builds this roster for a page the player
    // may never open, so the save is read on the first hire-page use instead of
    // at boot. Every public read waits for the one load, so no caller ever sees
    // a half-filled roster.
    private bool _loaded;

    public EmployeeRoster(IEmployeeStore store, Random rng)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rng);

        _store = store;
        _rng = rng;
    }

    /// <summary>The hired crew in hire order.</summary>
    public IReadOnlyList<string> Employees
    {
        get
        {
            EnsureLoaded();
            return _employees;
        }
    }

    /// <summary>The week's candidates, in the order the page shows them.</summary>
    public IReadOnlyList<string> Candidates
    {
        get
        {
            EnsureLoaded();
            return _candidates;
        }
    }

    /// <summary>The display week the current roster was rolled for, -1 before the first roll.</summary>
    public int RosterWeek
    {
        get
        {
            EnsureLoaded();
            return _rosterWeek;
        }
    }

    /// <summary>True when the name is already on the crew.</summary>
    public bool IsEmployed(string? name)
    {
        EnsureLoaded();
        string trimmed = (name ?? "").Trim();
        return trimmed.Length > 0 && IndexOf(_employees, trimmed) >= 0;
    }

    /// <summary>
    /// Unity's Show-time roster bookkeeping: a new display week re-rolls the
    /// five candidates from the residents the caller passes, while a return
    /// inside the same week keeps the roster and only tops it back up after a
    /// hire. A candidate who is no longer a resident is dropped; the crew is
    /// never offered, including after a hand-edited save. Writes only when
    /// something moved.
    /// </summary>
    public void RefreshCandidates(IReadOnlyList<string> residents, int week)
    {
        ArgumentNullException.ThrowIfNull(residents);
        EnsureLoaded();

        List<string> pool = Eligible(residents);
        bool changed = false;
        if (_rosterWeek != week)
        {
            _candidates.Clear();
            _candidates.AddRange(Draw(pool, CandidateCount));
            _rosterWeek = week;
            changed = true;
        }
        else
        {
            var eligible = new HashSet<string>(pool, NameComparer);
            changed |= _candidates.RemoveAll(name => !eligible.Contains(name)) > 0;
            if (_candidates.Count < CandidateCount)
            {
                var available = new List<string>(pool.Count);
                foreach (string name in pool)
                {
                    if (IndexOf(_candidates, name) < 0)
                    {
                        available.Add(name);
                    }
                }

                List<string> drawn = Draw(available, CandidateCount - _candidates.Count);
                if (drawn.Count > 0)
                {
                    _candidates.AddRange(drawn);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            Save();
        }
    }

    /// <summary>
    /// Hires one of this week's candidates: the name moves to the crew and
    /// leaves the roster, and the change is banked before the page reports it.
    /// False for a blank, unknown or already-hired name, so a stale row (the
    /// week turned while the page was open) can never hire twice.
    /// </summary>
    public bool Hire(string? name)
    {
        EnsureLoaded();
        string trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0 || IsEmployed(trimmed))
        {
            return false;
        }

        int index = IndexOf(_candidates, trimmed);
        if (index < 0)
        {
            return false;
        }

        string hired = _candidates[index];
        _candidates.RemoveAt(index);
        _employees.Add(hired);
        Save();
        return true;
    }

    /// <summary>The state the store banks.</summary>
    public EmployeeState Snapshot()
    {
        EnsureLoaded();
        return new EmployeeState([.. _employees], [.. _candidates], _rosterWeek);
    }

    // One guarded read on first use. The crew wins over a hand-edited candidate
    // list, so the page can never offer somebody who is already employed.
    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        EmployeeState saved = _store.Load();
        _employees = Clean(saved.Employees);
        _candidates = Clean(saved.Candidates);
        _rosterWeek = saved.RosterWeek;
        _loaded = true;
        _candidates.RemoveAll(name => IndexOf(_employees, name) >= 0);
    }

    // The residents a candidate may be drawn from: the caller's names, trimmed,
    // distinct and never somebody already on the crew. Order is the caller's
    // (the resident book's stable first-encounter order), so a seeded draw stays
    // deterministic.
    private List<string> Eligible(IReadOnlyList<string> residents)
    {
        var pool = new List<string>(residents.Count);
        var seen = new HashSet<string>(NameComparer);
        foreach (string? raw in residents)
        {
            string name = (raw ?? "").Trim();
            if (name.Length == 0 || IsEmployed(name) || !seen.Add(name))
            {
                continue;
            }

            pool.Add(name);
        }

        return pool;
    }

    // A uniform draw on the roster's own seeded stream: Unity's Shuffle + trim.
    private List<string> Draw(List<string> pool, int count)
    {
        var shuffled = new List<string>(pool);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        if (shuffled.Count > count)
        {
            shuffled.RemoveRange(count, shuffled.Count - count);
        }

        return shuffled;
    }

    private void Save() => _store.Save(Snapshot());

    private static int IndexOf(List<string> names, string name)
    {
        for (int i = 0; i < names.Count; i++)
        {
            if (NameComparer.Equals(names[i], name))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<string> Clean(IReadOnlyList<string>? names)
    {
        var cleaned = new List<string>();
        if (names is null)
        {
            return cleaned;
        }

        var seen = new HashSet<string>(NameComparer);
        foreach (string? raw in names)
        {
            string name = (raw ?? "").Trim();
            if (name.Length > 0 && seen.Add(name))
            {
                cleaned.Add(name);
            }
        }

        return cleaned;
    }
}
