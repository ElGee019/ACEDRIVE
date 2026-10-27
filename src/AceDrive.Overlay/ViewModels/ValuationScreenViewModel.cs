using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The weekly market-valuation page (PORTPLAN item 49, Unity ValuationScreen):
/// the same car picker as the garage, then the jury's comparison. The entry
/// phase lists every owned car and ENTER submits the pick; the comparison phase
/// shows the player's car and score beside a drawn competitor and the verdict,
/// and CONTINUE pays the verdict XP. The page never talks to the board or the
/// wallet: it reports the pick and the continuation to the flow, which owns the
/// entry, the draw and the payout.
/// </summary>
public sealed class ValuationScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly Garage _garage;
    private readonly IReadOnlyDictionary<string, CarSpec> _catalog;
    private readonly ICommand _closeCommand;
    private readonly Action<string> _onEnter;
    private readonly Action<double> _onContinue;
    private readonly VoiceBeat _beat;

    private bool _comparison;
    private bool _waiting;
    private double _pendingXp;
    private string _ownName = string.Empty;
    private string _ownCarKey = string.Empty;
    private double _ownScore;
    private ValuationEntry? _competitor;
    private ValuationVerdict? _verdict;
    private GarageRow? _selected;
    private IReadOnlyList<GarageStatRow> _stats = [];
    private bool _enterFlash;
    private bool _continueFlash;
    private bool _backFlash;

    public ValuationScreenViewModel(
        Garage garage, IReadOnlyDictionary<string, CarSpec> catalog, string venueLine,
        Action<string> onEnter, Action<double> onContinue, ICommand closeCommand,
        VoiceBeat beat)
    {
        ArgumentNullException.ThrowIfNull(garage);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(onEnter);
        ArgumentNullException.ThrowIfNull(onContinue);
        ArgumentNullException.ThrowIfNull(closeCommand);
        _garage = garage;
        _catalog = catalog;
        _onEnter = onEnter;
        _onContinue = onContinue;
        _closeCommand = closeCommand;
        _beat = beat;
        VenueLine = venueLine;
        Cars = BuildRows();
        _selected = InitialRow();
        EnterCommand = new RelayCommand(Enter, () => CanEnter);
        BackCommand = new RelayCommand(Back);
        ContinueCommand = new RelayCommand(Continue, () => HasCompetitor);
        Reload();
    }

    /// <summary>The venue this week's showcase is held at ("{street} {number}").</summary>
    public string VenueLine { get; }

    /// <summary>Unity's screen title.</summary>
    public string Title { get; } = Strings.Get("ValuationTitle");

    /// <summary>The car picker's caption.</summary>
    public string SelectLabel { get; } = Strings.Get("ValuationSelect");

    /// <summary>The empty-garage hint, so the page never reads as broken.</summary>
    public string EmptyLine { get; } = Strings.Get("ValuationNoCars");

    /// <summary>The entry phase's instruction line.</summary>
    public string IntroLine { get; } = Strings.Get("ValuationIntro");

    /// <summary>The owned cars, in garage order.</summary>
    public IReadOnlyList<GarageRow> Cars { get; }

    /// <summary>The car the stats block and ENTER read.</summary>
    public GarageRow? SelectedCar
    {
        get => _selected;
        set
        {
            if (SetField(ref _selected, value))
            {
                Reload();
            }
        }
    }

    /// <summary>The picked car's spec rows (price, engine, power, top speed, 0-60, class).</summary>
    public IReadOnlyList<GarageStatRow> Stats
    {
        get => _stats;
        private set => SetField(ref _stats, value);
    }

    /// <summary>False on an empty garage: nothing can be entered.</summary>
    public bool HasCars => Cars.Count > 0;

    /// <summary>True while the car picker is up (before ENTER).</summary>
    public bool IsEntry => !_comparison;

    /// <summary>True once ENTER moved the page to the comparison.</summary>
    public bool IsComparison => _comparison;

    /// <summary>True while the competitor list is still being read.</summary>
    public bool IsWaiting => _waiting;

    /// <summary>True once the competitor landed: the verdict and CONTINUE are up.</summary>
    public bool HasCompetitor => _competitor is not null;

    /// <summary>BACK only exists on the entry phase; CONTINUE is the way out after.</summary>
    public bool ShowBack => IsEntry;

    /// <summary>
    /// ENTER's label: plain "ENTER", never "ENTER WITH {car}". WHY: the label
    /// is also the voice key, and Vosk never hears a car title inside a button,
    /// so the long form made the tap unreachable by voice. The picked car stays
    /// visible in the picker, the stats block and the comparison.
    /// </summary>
    public string EnterLabel { get; } = Strings.Get("ValuationEnter");

    /// <summary>ENTER is live only with a pick on the entry phase.</summary>
    public bool CanEnter => IsEntry && _selected is not null;

    /// <summary>The own side of the comparison: the player's name.</summary>
    public string OwnName
    {
        get => _ownName;
        private set => SetField(ref _ownName, value);
    }

    /// <summary>The own side: the entered car's title.</summary>
    public string OwnCarName => CarTitles.Of(_ownCarKey, SpecOf(_ownCarKey));

    /// <summary>"YOUR SCORE: 123.45".</summary>
    public string OwnScoreText => Strings.Format(
        "ValuationOwnScore", Strings.Format("ValuationScore", _ownScore));

    /// <summary>The drawn competitor's name.</summary>
    public string OpponentName => _competitor?.PlayerName ?? string.Empty;

    /// <summary>The competitor's car title.</summary>
    public string OpponentCarName =>
        _competitor is { } entry ? CarTitles.Of(entry.CarKey, SpecOf(entry.CarKey)) : string.Empty;

    /// <summary>"OPPONENT'S SCORE: 98.10" (empty until the draw lands).</summary>
    public string OpponentScoreText => _competitor is { } entry
        ? Strings.Format(
            "ValuationOpponentScore", Strings.Format("ValuationScore", entry.Score))
        : string.Empty;

    /// <summary>"Drawing an opponent…" while the list load is in flight.</summary>
    public string WaitingText { get; } = Strings.Get("ValuationWaiting");

    /// <summary>"YOU WON" / "YOU LOST", empty before the draw.</summary>
    public string ResultText => _verdict is { } verdict
        ? Strings.Get(verdict.Won ? "ValuationWon" : "ValuationLost")
        : string.Empty;

    /// <summary>The jury sentence, empty before the draw.</summary>
    public string JuryText => _verdict is { } verdict
        ? Strings.Get(verdict.Won ? "ValuationJuryWon" : "ValuationJuryLost")
        : string.Empty;

    /// <summary>"You receive 0.12 XP", empty before the draw.</summary>
    public string EarningsText => _verdict is { } verdict
        ? Strings.Format("ValuationXp", verdict.Xp)
        : string.Empty;

    /// <summary>CONTINUE's label.</summary>
    public string ContinueLabel { get; } = Strings.Get("ValuationContinue");

    /// <summary>The entry phase's single verb.</summary>
    public ICommand EnterCommand { get; }

    /// <summary>Leaves the showcase without entering (entry phase only).</summary>
    public ICommand BackCommand { get; }

    /// <summary>Pays the verdict XP and closes the page.</summary>
    public ICommand ContinueCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash
    {
        get => _backFlash;
        private set => SetField(ref _backFlash, value);
    }

    /// <summary>Teal-glow acknowledgment for the ENTER voice tap.</summary>
    public bool EnterFlash
    {
        get => _enterFlash;
        private set => SetField(ref _enterFlash, value);
    }

    /// <summary>Teal-glow acknowledgment for the CONTINUE voice tap.</summary>
    public bool ContinueFlash
    {
        get => _continueFlash;
        private set => SetField(ref _continueFlash, value);
    }

    /// <summary>Spoken labels for both phases; cars answer by title.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>
    /// ENTER with the picked car: hands the key to the flow and flips the page
    /// to the waiting comparison. The flow may call <see cref="ShowCompetitor"/>
    /// as soon as the list lands (or a house fallback right away).
    /// </summary>
    private void Enter()
    {
        if (!CanEnter || _selected is not { } row)
        {
            return;
        }
        EnterFlash = false;
        _beat.Cancel();
        // The flow computes the score and calls ShowWaiting itself; the page
        // must not guess a score before the entry has been recorded.
        _onEnter(row.Key);
    }

    /// <summary>
    /// The flow started the entry: paints the own side and the waiting line.
    /// Called from the flow after it has the score, so the page never computes
    /// gameplay numbers.
    /// </summary>
    public void ShowWaiting(string playerName, string carKey, double ownScore)
    {
        _comparison = true;
        _waiting = true;
        _competitor = null;
        _verdict = null;
        _pendingXp = 0.0;
        OwnName = playerName;
        _ownCarKey = carKey;
        _ownScore = ownScore;
        RaisePhaseChanged();
    }

    /// <summary>The flow drew an opponent: paints it and the verdict.</summary>
    public void ShowCompetitor(ValuationEntry competitor, ValuationVerdict verdict)
    {
        _competitor = competitor;
        _verdict = verdict;
        _waiting = false;
        _pendingXp = verdict.Xp;
        RaisePhaseChanged();
    }

    /// <summary>CONTINUE: the verdict XP is paid once, then the page closes.</summary>
    private void Continue()
    {
        if (!HasCompetitor)
        {
            return;
        }
        ContinueFlash = false;
        double payout = _pendingXp;
        _pendingXp = 0.0;
        _onContinue(payout);
    }

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out.
    private void Back()
    {
        BackFlash = false;
        _closeCommand.Execute(null);
    }

    // Voice taps glow first and act at the end of the shared beat, like every
    // other page's buttons: the mic's acknowledgment stays visible before the
    // phase flips or the page leaves.
    private void FlashThenEnter()
    {
        EnterFlash = true;
        _beat.Defer(Enter);
    }

    private void FlashThenContinue()
    {
        ContinueFlash = true;
        _beat.Defer(Continue);
    }

    private void FlashThenBack()
    {
        BackFlash = true;
        _beat.Defer(Back);
    }

    private void Reload()
    {
        CarSpec? spec = _selected is null ? null : SpecOf(_selected.Key);
        Stats = BuildStats(spec);
        OnPropertyChanged(nameof(CanEnter));
        // The picker's selection drives ENTER's can-execute, so the button
        // re-arms the moment a car is chosen instead of on the next paint.
        ((RelayCommand)EnterCommand).RaiseCanExecuteChanged();
        RebuildVoice();
    }

    private void RaisePhaseChanged()
    {
        OnPropertyChanged(nameof(IsEntry));
        OnPropertyChanged(nameof(IsComparison));
        OnPropertyChanged(nameof(IsWaiting));
        OnPropertyChanged(nameof(HasCompetitor));
        OnPropertyChanged(nameof(ShowBack));
        OnPropertyChanged(nameof(OwnCarName));
        OnPropertyChanged(nameof(OpponentName));
        OnPropertyChanged(nameof(OpponentCarName));
        OnPropertyChanged(nameof(OpponentScoreText));
        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(JuryText));
        OnPropertyChanged(nameof(EarningsText));
        OnPropertyChanged(nameof(OwnScoreText));
        // Both verbs read this phase: ENTER only exists before the entry and
        // CONTINUE only once the opponent landed, so their can-execute moved.
        ((RelayCommand)EnterCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ContinueCommand).RaiseCanExecuteChanged();
        RebuildVoice();
    }

    private List<GarageRow> BuildRows()
    {
        List<GarageRow> rows = new(_garage.Cars.Count);
        foreach (OwnedCar car in _garage.Cars)
        {
            rows.Add(new GarageRow(
                car.Key,
                CarTitles.Of(car.Key, _garage.SpecOf(car)),
                string.Equals(car.Key, _garage.CurrentKey, StringComparison.Ordinal)));
        }
        return rows;
    }

    private GarageRow? InitialRow()
    {
        foreach (GarageRow row in Cars)
        {
            if (row.IsCurrent)
            {
                return row;
            }
        }
        return Cars.Count > 0 ? Cars[0] : null;
    }

    private CarSpec? SpecOf(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }
        if (_catalog.TryGetValue(key, out CarSpec? spec))
        {
            return spec;
        }
        return _garage.Find(key) is { } car ? _garage.SpecOf(car) : null;
    }

    private static IReadOnlyList<GarageStatRow> BuildStats(CarSpec? spec)
    {
        if (spec is null)
        {
            return [];
        }
        return
        [
            new(Strings.Get("GaragePriceLabel"), Strings.Format("GaragePrice", spec.Price)),
            new(Strings.Get("GarageEngineLabel"), Strings.Format("GarageEngine", spec.EngineCc)),
            new(Strings.Get("GaragePowerLabel"), Strings.Format("GaragePower", spec.Horsepower)),
            new(Strings.Get("ValuationTopSpeedLabel"), Strings.Format("ValuationTopSpeed", spec.TopSpeedKmh)),
            new(Strings.Get("ValuationAccelerationLabel"), Strings.Format("ValuationAcceleration", spec.Acceleration)),
            new(Strings.Get("ValuationClassLabel"), Strings.Format("ValuationClass", spec.Class)),
        ];
    }

    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>();
        foreach (GarageRow row in Cars)
        {
            if (row is null) continue;
            actions[row.Title] = () => SelectedCar = row;
        }

        if (IsEntry)
        {
            actions[EnterLabel] = FlashThenEnter;
            actions[Strings.Get("ValuationBack")] = FlashThenBack;
        }
        else if (HasCompetitor)
        {
            actions[ContinueLabel] = FlashThenContinue;
        }
        VoiceActions = actions;
    }
}
