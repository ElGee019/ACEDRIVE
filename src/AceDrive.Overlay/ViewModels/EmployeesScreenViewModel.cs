using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The employees page behind the menu's EMPLOYEES tile (PORTPLAN item 23,
/// Unity EmployeeScreen's employee section): the hired crew only. Each row
/// shows the employee's name on the left and, on the right, NOT ON JOB (so the
/// player can assign that employee a job), ON JOB while the employee works, or
/// ARRIVED once the job's due minute has passed. ARRIVED banks the job's pay —
/// car sharing in XP, the taxi and delivery runs in CR — replaces the name with
/// "{Name} earned {amount} ..." for three seconds, then returns the row to
/// NOT ON JOB.
///
/// A job takes one car out of the garage, and the player's own car is the one
/// being driven, so an idle row only offers NOT ON JOB while an unused car is
/// left (<see cref="Garage.Cars"/> minus the current car minus the employees on
/// a running job). With every car in use the page hides the buttons and paints
/// "No unused cars in your garage." above the list instead: the player can still
/// collect an ARRIVED job, because that job's car has come back.
///
/// The page is rebuilt per visit (like the home page), so the crew list and the
/// job marks are a live reading. Opening it is also when a car-sharing
/// subscription waiting for work picks up the town's real batch
/// (<see cref="CarShareJobs"/>): the schedule is set every night, so a quiet-day
/// assignment only finishes once rides are actually booked. A job can also come
/// due while the page stays open, so the page opts into the one 10 Hz loop and
/// lets each row flip its own state; the same tick expires the reward line and
/// returns the freed car. BACK returns to the menu it was opened from, with the
/// shared voice beat.
/// </summary>
public sealed class EmployeesScreenViewModel
    : ViewModelBase, IVoiceScreen, IRefreshableScreen, IPolledScreen
{
    private readonly Garage _garage;
    private readonly EmployeeAssignments _assignments;
    private readonly CarShareJobs _carShare;
    private readonly EmployeeDeliveryPayout _deliveryPayout;
    private readonly Func<IReadOnlyList<CarShareRequest>> _carShareBatch;
    private readonly GameClock _game;
    private readonly IClock _clock;
    private readonly Action<double> _bankXp;
    private readonly Action<double> _bankCr;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;
    // TEMP (testing): lets the composition site switch the no-unused-car gate
    // off while the assign flow is exercised on a one-car save. The default is
    // on, so the rule and its tests stay authoritative; remove the switch to
    // restore the gate everywhere.
    private readonly bool _enforceUnusedCars;

    public EmployeesScreenViewModel(
        EmployeeRoster roster,
        Garage garage,
        EmployeeAssignments assignments,
        CarShareJobs carShare,
        EmployeeDeliveryPayout deliveryPayout,
        Func<IReadOnlyList<CarShareRequest>> carShareBatch,
        GameClock game,
        IClock clock,
        Action<double> bankXp,
        Action<double> bankCr,
        ICommand notOnJobCommand,
        ICommand backCommand,
        VoiceBeat beat,
        bool enforceUnusedCars = true)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(garage);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(carShare);
        ArgumentNullException.ThrowIfNull(deliveryPayout);
        ArgumentNullException.ThrowIfNull(carShareBatch);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(bankXp);
        ArgumentNullException.ThrowIfNull(bankCr);
        ArgumentNullException.ThrowIfNull(notOnJobCommand);
        ArgumentNullException.ThrowIfNull(backCommand);
        ArgumentNullException.ThrowIfNull(beat);

        _garage = garage;
        _assignments = assignments;
        _carShare = carShare;
        _deliveryPayout = deliveryPayout;
        _carShareBatch = carShareBatch;
        _game = game;
        _clock = clock;
        _bankXp = bankXp;
        _bankCr = bankCr;
        _backCommand = backCommand;
        _beat = beat;
        _enforceUnusedCars = enforceUnusedCars;
        Roster = roster;
        NotOnJobCommand = notOnJobCommand;
        ArrivedCommand = new ParameterizedCommand<string>(Collect);
        BackCommand = new RelayCommand(Back);
        Refresh();
        RebuildVoice();
    }

    /// <summary>The hired crew, read live so a hire shows on the next visit.</summary>
    public EmployeeRoster Roster { get; }

    /// <summary>The employee rows in hire order; empty before the first hire.</summary>
    public IReadOnlyList<EmployeeRow> Employees
    {
        get => _employees;
        private set => SetField(ref _employees, value);
    }
    private IReadOnlyList<EmployeeRow> _employees = [];

    /// <summary>True when the page has a crew row to paint.</summary>
    public bool HasEmployees => Employees.Count > 0;

    /// <summary>The empty-page hint, so a fresh career never reads as broken.</summary>
    public bool IsEmpty => !HasEmployees;

    /// <summary>The line the empty page shows.</summary>
    public string EmptyLine { get; } = Strings.Get("EmployeesEmpty");

    /// <summary>
    /// The line the page shows above the crew when no car is left to assign:
    /// the player's own car is in use and every other owned car is out with an
    /// employee, so no idle row can take a job.
    /// </summary>
    public string NoUnusedCarsLine { get; } = Strings.Get("EmployeesNoUnusedCars");

    /// <summary>
    /// True while the crew is listed and no unused car remains. The page then
    /// paints <see cref="NoUnusedCarsLine"/> and every idle row hides its NOT ON
    /// JOB button. An empty crew keeps the empty-page hint instead: there is
    /// nobody to assign and no reason to talk about cars.
    /// </summary>
    public bool ShowNoUnusedCars
    {
        get => _showNoUnusedCars;
        private set => SetField(ref _showNoUnusedCars, value);
    }
    private bool _showNoUnusedCars;

    /// <summary>One row's NOT ON JOB button, keyed by the employee's name.</summary>
    public ICommand NotOnJobCommand { get; }

    /// <summary>One row's ARRIVED button, keyed by the employee's name.</summary>
    public ICommand ArrivedCommand { get; }

    /// <summary>The page's way back to the menu.</summary>
    public ICommand BackCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash
    {
        get => _backFlash;
        private set => SetField(ref _backFlash, value);
    }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>
    /// Re-reads the crew and each employee's job mark. The page is built fresh
    /// per visit, but an interrupting page can hand the same instance back
    /// after the player returns from the assign page, so the presenter's
    /// hand-back hook repaints the rows.
    ///
    /// A car-sharing row still waiting for work is armed from the town's real
    /// batch here: this is the cold path that turns a quiet-day assignment into
    /// a shift once the nightly schedule books rides, and it costs one batch
    /// read per page open rather than a write on the poll.
    /// </summary>
    public void Refresh()
    {
        _carShare.ArmWaiting(Roster.Employees, _carShareBatch());

        long gameMinute = _game.GameMinute;
        bool hasUnusedCars = HasUnusedCar(gameMinute);
        var rows = new List<EmployeeRow>(Roster.Employees.Count);
        foreach (string name in Roster.Employees)
        {
            rows.Add(new EmployeeRow(
                name, _assignments.AssignmentOf(name), gameMinute, hasUnusedCars));
        }

        Employees = rows;
        ShowNoUnusedCars = HasEmployees && !hasUnusedCars;
        RebuildVoice();
    }

    /// <summary>
    /// Advances every row to the current game minute, re-reads the free car and
    /// expires a collected line after its three seconds. Runs on the one allowed
    /// loop. A job coming due returns its car here, so the idle rows' buttons
    /// come back without a page rebuild.
    /// </summary>
    public void Tick(DateTime now)
    {
        long gameMinute = _game.GameMinute;
        bool hasUnusedCars = HasUnusedCar(gameMinute);
        ShowNoUnusedCars = HasEmployees && !hasUnusedCars;
        foreach (EmployeeRow row in Employees)
        {
            row.Tick(
                _assignments.AssignmentOf(row.Name), gameMinute, now, hasUnusedCars);
        }
    }

    // The garage rule behind the page: the car the player drives is never
    // unused, and each running job takes one more. A job whose due minute has
    // passed has returned its car, so its row is ARRIVED and waits for the
    // collect while another employee may take the car.
    private bool HasUnusedCar(long gameMinute)
    {
        if (!_enforceUnusedCars)
        {
            return true;
        }

        int inUse = 1; // the current car, the one the player drives
        foreach (string name in Roster.Employees)
        {
            if (_assignments.AssignmentOf(name) is { } job && !job.IsDue(gameMinute))
            {
                inUse++;
            }
        }

        return _garage.Cars.Count > inUse;
    }

    // Collecting is the row's ARRIVED tap: take the finished job, bank what it
    // earned, and let the row paint the reward line. A taxi ride and a
    // car-sharing batch already carry their amount; a delivery only knows its
    // store at assignment, so its payout is settled here against the live
    // shelves — one CR per unit the refill adds, Worker Bonus doubling it.
    private void Collect(string? name)
    {
        if (_assignments.TakeFinished(name, _game.GameMinute) is not { } job)
        {
            return;
        }

        double cr = job.Cr;
        if (job.StoreKey is { Length: > 0 } storeKey)
        {
            cr = _deliveryPayout.Settle(storeKey).Cr;
        }

        // A family pays one currency, so only one of the two amounts is
        // non-zero; the zero one is skipped rather than banked, because a bank
        // writes the driver file and a nothing-earned tap must not.
        if (job.Xp > 0.0)
        {
            _bankXp(job.Xp);
        }

        if (cr > 0.0)
        {
            _bankCr(cr);
        }

        FindRow(name)?.BeginEarning(EarnedLine(name, job, cr), _clock.UtcNow);
    }

    // The collected line's currency follows the family: car sharing returns the
    // day's fractional XP, the taxi and delivery runs return CR. The page owns
    // the wording so the row stays a line holder and a translator reads both
    // keys side by side.
    private static string EarnedLine(string? name, EmployeeJobAssignment job, double cr) =>
        job.Kind == EmployeeJobKind.CarSharing
            ? Strings.Format("EmployeesEarnedXp", name, job.Xp)
            : Strings.Format("EmployeesEarnedCr", name, cr);

    private EmployeeRow? FindRow(string? name)
    {
        foreach (EmployeeRow row in Employees)
        {
            if (string.Equals(row.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return null;
    }

    // One path back: a click and the voice tap's deferred end both land here,
    // so the glow clears on the way out.
    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }

    private void RebuildVoice()
    {
        VoiceActions = new Dictionary<string, Action>
        {
            // Back navigates: glow first, act at the end of the shared beat.
            [Strings.Get("EmployeesBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
    }
}
