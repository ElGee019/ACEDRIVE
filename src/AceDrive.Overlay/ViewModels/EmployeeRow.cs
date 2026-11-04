using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One employee row on the employees page: the hire name plus the three states
/// the row can paint on the right — NOT ON JOB while idle, ON JOB while the
/// assignment runs, and ARRIVED once the game clock reaches its due minute.
/// Collecting an arrival replaces the name with the earned line for
/// <see cref="RewardDuration"/>, then the row falls back to idle.
///
/// The row is rebuilt by the page's refresh, so it owns no assignment: the page
/// hands it the live one on every tick and the row only tracks whether the
/// state changed, which keeps the 10 Hz repaint free of allocations.
///
/// A job takes a car out of the garage, so the page also hands each row whether
/// an unused car is left. An idle row only paints NOT ON JOB while one is: the
/// page's "No unused cars in your garage." line then explains the quiet rows
/// instead of leaving the player with a button that cannot lead anywhere.
/// </summary>
public sealed class EmployeeRow : ViewModelBase
{
    /// <summary>How long the collected line replaces the employee's name.</summary>
    public static TimeSpan RewardDuration { get; } = TimeSpan.FromSeconds(3);

    private readonly string _name;
    private bool _onJob;
    private bool _arrived;
    private bool _carsAvailable;
    private string? _earnedLine;
    private DateTime _earnedUntil;

    public EmployeeRow(
        string name, EmployeeJobAssignment? assignment, long gameMinute, bool carsAvailable)
    {
        _name = name;
        _carsAvailable = carsAvailable;
        Snap(assignment, gameMinute);
    }

    /// <summary>The hire name, and the key the page's commands carry.</summary>
    public string Name => _name;

    /// <summary>The line painted on the left: the name, or the collected reward.</summary>
    public string DisplayName => _earnedLine ?? _name;

    /// <summary>True while the employee's job is running.</summary>
    public bool OnJob => _onJob;

    /// <summary>True once the job is due and waits for the player's collect.</summary>
    public bool Arrived => _arrived;

    /// <summary>True while the reward line replaces the name.</summary>
    public bool IsEarning => _earnedLine is not null;

    /// <summary>
    /// True while the row still paints the NOT ON JOB button: idle and earning
    /// nothing, with an unused car left in the garage to take.
    /// </summary>
    public bool ShowNotOnJob => _carsAvailable && !_onJob && !_arrived && !IsEarning;

    /// <summary>
    /// Re-reads the assignment, the free car and lets the reward line expire.
    /// Called on the one 10 Hz loop; repaints only when a flag actually changed.
    /// </summary>
    public void Tick(
        EmployeeJobAssignment? assignment, long gameMinute, DateTime now, bool carsAvailable)
    {
        bool changed = Snap(assignment, gameMinute);
        if (_carsAvailable != carsAvailable)
        {
            _carsAvailable = carsAvailable;
            changed = true;
        }

        if (changed)
        {
            OnPropertyChanged(nameof(ShowNotOnJob));
        }

        if (_earnedLine is not null && now >= _earnedUntil)
        {
            _earnedLine = null;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(IsEarning));
            OnPropertyChanged(nameof(ShowNotOnJob));
        }
    }

    /// <summary>
    /// Shows what the just-collected job paid, in place of the name, for the
    /// reward window. The page formats the line (XP or CR, per family) and has
    /// already banked it.
    /// </summary>
    public void BeginEarning(string earnedLine, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(earnedLine);

        _onJob = false;
        _arrived = false;
        _earnedLine = earnedLine;
        _earnedUntil = now + RewardDuration;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(OnJob));
        OnPropertyChanged(nameof(Arrived));
        OnPropertyChanged(nameof(IsEarning));
        OnPropertyChanged(nameof(ShowNotOnJob));
    }

    // Snap only tracks the state; the caller decides when the derived
    // ShowNotOnJob needs repainting, so one tick raises it at most once.
    private bool Snap(EmployeeJobAssignment? assignment, long gameMinute)
    {
        bool due = assignment?.IsDue(gameMinute) ?? false;
        bool changed = SetField(ref _arrived, due, nameof(Arrived));
        changed |= SetField(ref _onJob, assignment is not null && !due, nameof(OnJob));
        return changed;
    }
}
