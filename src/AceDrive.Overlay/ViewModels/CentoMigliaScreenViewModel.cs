using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The Cento Miglia page (PORTPLAN item 49, port of Unity CentoMigliaScreen's
/// tier list, detail panel and ENTER flow). Sunday's calendar row opens it
/// inside the 8am-8pm gate; the page prints the week's club reputation, the
/// eight tiers with the level each asks for, and — once a tier is picked — its
/// description and the driver's eligibility lines.
///
/// ENTER EVENT builds the event the way Unity's screen does: the tier issues a
/// registration number (its digit count grows with the club), the page reports
/// the build countdown for Unity's minimum setup delay and then shows the
/// built route — the longest distance the player's operating zone allows. No
/// way out exists while the event is being prepared; past the build, BACK
/// leaves the page. The race itself (NPC field, finish, receipt) is the next
/// slice, so the built event travels to the host as one record instead of
/// starting a drive.
///
/// Unity's weekly one-entry gate is absent with the race: it is written by a
/// finished race. The header keeps Unity's reputation floor (a raw 0 prints as
/// 1.00000) and the detail panel keeps its three lines; the assertivity line
/// stays out for the same reason it does on the customer-request page (the stat
/// has no port yet).
/// </summary>
public sealed class CentoMigliaScreenViewModel : ViewModelBase, IVoiceScreen, IPolledScreen
{
    private readonly CentoMigliaFacts _facts;
    // The page's one way out: BACK on the list and ENTER on a tier's built
    // event both close it. Unity pops back to the jobs page, which is the
    // calendar here.
    private readonly ICommand _close;
    // The build countdown is wall time, like the request timer: the deadline is
    // stamped once and the poll only formats what is left of it.
    private readonly IClock _clock;
    // The registration draw is the page's only randomness; the tier (one to
    // eight) only decides how many digits it has.
    private readonly Random _rng;
    // The built event is the race port's input. Until that port exists the host
    // logs it, so this page never grows an I/O or logging dependency.
    private readonly Action<CentoMigliaEvent> _onBuilt;
    private CentoTierRow? _selected;
    private DateTime? _buildDeadline;
    private bool _built;
    private int _registration;
    private double _distanceKm;
    private int _remainingSeconds;

    public CentoMigliaScreenViewModel(
        IReadOnlyList<CentoTier> tiers, CentoMigliaFacts facts, ICommand close,
        IClock clock, Random rng, Action<CentoMigliaEvent> onBuilt)
    {
        _facts = facts;
        _clock = clock;
        _rng = rng;
        _onBuilt = onBuilt;
        _close = close;
        Tiers = BuildRows(tiers, facts);
        WeekLine = Strings.Format("CentoWeek", facts.Week);
        ReputationLine = Strings.Format(
            "CentoReputation", CentoMiglia.DisplayReputation(facts.ClubReputation));
        TierCommand = new ParameterizedCommand<CentoTierRow>(OpenTier);
        BackCommand = new RelayCommand(Back);
        EnterCommand = new RelayCommand(Enter);
        RebuildVoice();
    }

    /// <summary>The club rows in the data file's order (Unity's button order).</summary>
    public IReadOnlyList<CentoTierRow> Tiers { get; }

    /// <summary>"Week {n}" - Unity's TextDate, without the year.</summary>
    public string WeekLine { get; }

    /// <summary>"YOUR CLUB REPUTATION: 1.00000" - Unity's TextClubReputation.</summary>
    public string ReputationLine { get; }

    /// <summary>The tier whose detail panel is open; null shows the list.</summary>
    public CentoTierRow? Selected
    {
        get => _selected;
        private set
        {
            if (!SetField(ref _selected, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsTierList));
            OnPropertyChanged(nameof(IsDetail));
            OnPropertyChanged(nameof(SetupVisible));
            OnPropertyChanged(nameof(DetailLabel));
            OnPropertyChanged(nameof(DetailDescription));
            OnPropertyChanged(nameof(EligibilityText));
            OnPropertyChanged(nameof(ShowEnter));
            RebuildVoice();
        }
    }

    /// <summary>True on the tier list (Unity's CentoA panel).</summary>
    public bool IsTierList => _selected is null;

    /// <summary>True on a tier's detail (Unity's CentoB panel), before it is
    /// entered.</summary>
    public bool IsDetail => _selected is not null && _buildDeadline is null;

    /// <summary>True while the picked tier's event is being built: the
    /// registration number is up and the countdown is running.</summary>
    public bool IsBuilding => _buildDeadline is not null && !_built;

    /// <summary>True once the build finished: the event shows its route.</summary>
    public bool IsBuilt => _built;

    /// <summary>True once ENTER started the build, through to the built event:
    /// the panel replaces the list and the detail.</summary>
    public bool SetupVisible => _buildDeadline is not null;

    /// <summary>The build panel's heading: the event being prepared, then the
    /// ready event.</summary>
    public string SetupTitle => _built
        ? Strings.Get("CentoBuiltTitle")
        : Strings.Get("CentoBuildingTitle");

    /// <summary>The open tier's label: the detail panel's heading and the
    /// built event's club.</summary>
    public string DetailLabel => _selected?.Label ?? string.Empty;

    /// <summary>The open tier's description (content, never resx).</summary>
    public string DetailDescription => _selected?.Tier.Description ?? string.Empty;

    /// <summary>"Registration #12" - Unity's TextRegistration.</summary>
    public string RegistrationLine => SetupVisible
        ? Strings.Format("CentoRegistration", _registration)
        : string.Empty;

    /// <summary>The build countdown ("Starting in 3s"), Unity's minimum setup
    /// delay made visible.</summary>
    public string TimerLine => IsBuilding
        ? Strings.Format("CentoBuildTimer", _remainingSeconds)
        : string.Empty;

    /// <summary>The built route ("Longest distance: 1.9 km"): the whole
    /// operating zone, which is the longest route it allows.</summary>
    public string DistanceLine => Strings.Format("CentoEventDistance", _distanceKm);

    /// <summary>ENTER's label: Unity swaps it for "SETTING UP…" while the
    /// event is prepared.</summary>
    public string EnterLabel => IsBuilding
        ? Strings.Get("CentoSettingUp")
        : Strings.Get("CentoEnter");

    /// <summary>ENTER shows on the detail (to start the build) and while the
    /// build runs (disabled, as its status line); the built event's way out is
    /// BACK.</summary>
    public bool ShowEnter => IsDetail || IsBuilding;

    /// <summary>BACK is hidden while the event is being prepared.</summary>
    public bool ShowBack => !IsBuilding;

    /// <summary>ENTER only acts on the detail: one build per entry.</summary>
    public bool CanEnter => IsDetail;

    /// <summary>Opens a tier's detail; a locked row never gets here (the button
    /// is disabled), so the guard only answers a stale command.</summary>
    public ICommand TierCommand { get; }

    /// <summary>BACK: the detail returns to the list, the built event and the
    /// list leave the page.</summary>
    public ICommand BackCommand { get; }

    /// <summary>ENTER EVENT: builds the picked tier's event.</summary>
    public ICommand EnterCommand { get; }

    /// <summary>
    /// Spoken text -&gt; action. The list answers to each unlocked tier's label
    /// and BACK; the detail answers to ENTER EVENT and BACK. While the event is
    /// built nothing answers: Unity hides BACK and disables the enter button, so
    /// a spoken word has no button to press. Locked tiers stay out, like their
    /// disabled buttons.
    /// </summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>
    /// The build countdown on integer seconds, ticked by the host's poll. The
    /// finished build reports itself once, so a page left on screen never
    /// rebuilds the same event twice.
    /// </summary>
    public void Tick(DateTime now)
    {
        if (_buildDeadline is not { } deadline || _built)
        {
            return;
        }

        double left = (deadline - now).TotalSeconds;
        int seconds = left <= 0.0
            ? 0
            : (int)Math.Ceiling(Math.Min(left, CentoMiglia.BuildSeconds));
        if (seconds != _remainingSeconds)
        {
            _remainingSeconds = seconds;
            OnPropertyChanged(nameof(TimerLine));
        }

        if (seconds == 0)
        {
            FinishBuild();
        }
    }

    /// <summary>
    /// The detail panel's eligibility block, one line each like Unity's
    /// TextEligibility: the reputation the tier asks for, the garage size and
    /// the current car's class. Unity's fourth line (assertivity) has no stat
    /// behind it in this port.
    /// </summary>
    public string EligibilityText
    {
        get
        {
            if (_selected is not { } row)
            {
                return string.Empty;
            }

            string reputation = row.Tier.Id == CentoMiglia.GentlemenDrivers
                ? Strings.Get("CentoFreeEntry")
                : Strings.Format(
                    "CentoEligibilityReputation",
                    _facts.ClubReputation, row.Tier.ReputationRequired);
            string cars = Strings.Format(
                _facts.CarsOwned == 1 ? "CentoOwnsCar" : "CentoOwnsCars",
                _facts.CarsOwned);
            return string.Join(
                '\n', reputation, cars, Strings.Format("CentoCarClass", _facts.CarClass));
        }
    }

    private static CentoTierRow[] BuildRows(
        IReadOnlyList<CentoTier> tiers, CentoMigliaFacts facts)
    {
        int decade = CentoMiglia.EraDecade(facts.Year);
        var rows = new CentoTierRow[tiers.Count];
        for (int i = 0; i < rows.Length; i++)
        {
            CentoTier tier = tiers[i];
            rows[i] = new CentoTierRow(
                tier,
                string.Format(CultureInfo.CurrentCulture, tier.Label, decade),
                Strings.Format("CentoTierRequirement", tier.ReputationRequired),
                CentoMiglia.IsTierEligible(
                    tier, facts.ClubReputation, facts.HasDiscoveredStreets),
                i + 1);
        }
        return rows;
    }

    private void OpenTier(CentoTierRow? row)
    {
        if (row is null || !row.Eligible)
        {
            return;
        }

        Selected = row;
    }

    // Unity's OnEnterEventClicked: the tier issues its registration number at
    // once (the player reads it before the race), the route is the zone's
    // longest, and the page holds the player for the minimum setup delay.
    private void Enter()
    {
        if (_selected is not { } row || _buildDeadline is not null)
        {
            return;
        }

        _registration = CentoMiglia.DrawRegistration(row.Number, _rng);
        _distanceKm = CentoMiglia.LongestDistanceKm(_facts.ZoneKm);
        _remainingSeconds = CentoMiglia.BuildSeconds;
        _buildDeadline = _clock.UtcNow.AddSeconds(CentoMiglia.BuildSeconds);
        NotifySetupChanged();
        RebuildVoice();
    }

    private void FinishBuild()
    {
        if (_built || _selected is not { } row)
        {
            return;
        }

        _built = true;
        NotifySetupChanged();
        RebuildVoice();
        _onBuilt(new CentoMigliaEvent(row.Number, row.Label, _registration, _distanceKm));
    }

    // The build swaps the body panel and both bottom-band buttons at once, so
    // the transition notifies every affected member together instead of leaving
    // a half-painted page.
    private void NotifySetupChanged()
    {
        OnPropertyChanged(nameof(IsDetail));
        OnPropertyChanged(nameof(IsBuilding));
        OnPropertyChanged(nameof(IsBuilt));
        OnPropertyChanged(nameof(SetupVisible));
        OnPropertyChanged(nameof(SetupTitle));
        OnPropertyChanged(nameof(RegistrationLine));
        OnPropertyChanged(nameof(TimerLine));
        OnPropertyChanged(nameof(DistanceLine));
        OnPropertyChanged(nameof(EnterLabel));
        OnPropertyChanged(nameof(ShowEnter));
        OnPropertyChanged(nameof(ShowBack));
        OnPropertyChanged(nameof(CanEnter));
    }

    private void Back()
    {
        // Unity blocks the exit while the event is being prepared; once built,
        // BACK is the page's way out (Unity's race would have taken over).
        if (IsBuilding)
        {
            return;
        }

        if (IsDetail)
        {
            Selected = null;
            return;
        }

        _close.Execute(null);
    }

    private void RebuildVoice()
    {
        if (IsBuilding)
        {
            VoiceActions = new Dictionary<string, Action>();
            return;
        }

        var actions = new Dictionary<string, Action>
        {
            [Strings.Get("DeliveryBack")] = Back,
        };
        if (IsBuilt)
        {
            VoiceActions = actions;
            return;
        }

        if (_selected is not null)
        {
            actions[Strings.Get("CentoEnter")] = Enter;
        }
        else
        {
            foreach (CentoTierRow row in Tiers)
            {
                if (row.Eligible)
                {
                    actions[row.Label] = () => OpenTier(row);
                }
            }
        }
        VoiceActions = actions;
    }
}
