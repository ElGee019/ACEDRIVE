// The employees page's on-job state (PORTPLAN item 23). The page reads it to
// paint each row's NOT ON JOB button, ON JOB text or ARRIVED button, and the
// assign page writes it when the player picks a work family. Every change is
// banked through IEmployeeJobStore, so a restart reopens the crew at work instead
// of every row idle: the due minute is an absolute game minute and the game clock
// resumes from its own save, so a timed job keeps its remaining time across
// launches and a player who left one collecting finds it ARRIVED.
namespace AceDrive.Core;

/// <summary>
/// Which employees are currently on a job, keyed by hire name. One assignment
/// per employee, no counts: the page only needs the row's state.
///
/// Read once on first use and written on every change (assign, collect, cancel),
/// the same cache-first shape <see cref="EmployeeRoster"/> uses: the 10 Hz row
/// tick then reads memory only, and the store is touched on the first page open
/// of the session plus one write per tap.
/// </summary>
public sealed class EmployeeAssignments
{
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IEmployeeJobStore _store;
    private readonly Dictionary<string, EmployeeJobAssignment> _onJob = new(NameComparer);
    // WHY lazy: the composition root builds this for a page the player may never
    // open, so the save is read on the first employees-page open instead of at
    // boot. Every public read and write waits for that one load, so a write can
    // never clobber jobs it has not read yet.
    private bool _loaded;

    public EmployeeAssignments(IEmployeeJobStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>True while this employee has a job (their row is not idle).</summary>
    public bool IsOnJob(string? name) => AssignmentOf(name) is not null;

    /// <summary>The job the employee is on, or null while they are not on one.</summary>
    public EmployeeJobAssignment? AssignmentOf(string? name)
    {
        string trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        EnsureLoaded();
        return _onJob.TryGetValue(trimmed, out EmployeeJobAssignment? job) ? job : null;
    }

    /// <summary>The work family the employee is on, or null while they are not on a job.</summary>
    public EmployeeJobKind? JobOf(string? name) => AssignmentOf(name)?.Kind;

    /// <summary>
    /// Marks the employee as on the given work family with no finish model. The
    /// employees page then paints ON JOB instead of the NOT ON JOB button.
    /// </summary>
    public void Assign(string? name, EmployeeJobKind kind) =>
        Assign(name, new EmployeeJobAssignment(kind));

    /// <summary>
    /// Marks the employee as on a timed job: the row flips to ARRIVED at
    /// <paramref name="dueGameMinute"/> and collecting banks
    /// <paramref name="xp"/>.
    /// </summary>
    public void Assign(string? name, EmployeeJobKind kind, long dueGameMinute, double xp) =>
        Assign(name, new EmployeeJobAssignment(kind, dueGameMinute, xp));

    /// <summary>
    /// Puts the employee on a job the caller has already shaped, so a family
    /// whose finish depends on the day (<see cref="CarShareJobs"/>) decides it
    /// once instead of the page re-deriving it.
    /// </summary>
    public void Assign(string? name, EmployeeJobAssignment job)
    {
        ArgumentNullException.ThrowIfNull(job);

        string trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        EnsureLoaded();
        _onJob[trimmed] = job;
        Save();
    }

    /// <summary>
    /// Takes the employee off a job whose due minute has passed and returns what
    /// that job was, or null when the employee has nothing to collect. The
    /// caller banks the returned XP; the row then falls back to NOT ON JOB.
    ///
    /// WHY the removal banks before the caller pays: a crash in that window loses
    /// one job's XP, while the other order would leave the job collectable after
    /// a restart and pay it twice.
    /// </summary>
    public EmployeeJobAssignment? TakeFinished(string? name, long gameMinute)
    {
        EmployeeJobAssignment? job = AssignmentOf(name);
        if (job is null || !job.IsDue(gameMinute))
        {
            return null;
        }

        Remove(name);
        return job;
    }

    /// <summary>
    /// Takes the employee off their job without collecting, so their row's NOT
    /// ON JOB button returns. A cancelled job uses this path.
    /// </summary>
    public void Clear(string? name) => Remove(name);

    /// <summary>
    /// Takes one employee off whatever they are on and banks the change. A name
    /// with no job writes nothing, so a stray tap costs no disk write.
    /// </summary>
    private void Remove(string? name)
    {
        string trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        EnsureLoaded();
        if (_onJob.Remove(trimmed))
        {
            Save();
        }
    }

    // One guarded read on first use; a job for a blank name is dropped so a
    // hand-edited save cannot paint a row nothing owns.
    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        EmployeeJobState saved = _store.Load();
        if (saved.Jobs is not null)
        {
            foreach ((string raw, EmployeeJobAssignment job) in saved.Jobs)
            {
                string name = (raw ?? "").Trim();
                if (name.Length > 0)
                {
                    _onJob[name] = job;
                }
            }
        }

        _loaded = true;
    }

    // The live map is copied so the store never aliases state it does not own:
    // a failed write must leave the page reading what it just changed.
    private void Save() =>
        _store.Save(new EmployeeJobState(
            new Dictionary<string, EmployeeJobAssignment>(_onJob, NameComparer)));
}
