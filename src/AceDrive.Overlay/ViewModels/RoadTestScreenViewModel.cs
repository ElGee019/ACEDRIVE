using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// La Strada's road-test page (PORTPLAN item 49, Unity RoadTestScreen): the
/// week's car under the title, the two legs Unity offers (0-60 and 100-0) with
/// their briefs, and the photo mission button that only appears once both legs
/// have been driven; after a run, the finish sentence, the car's board and the
/// all-cars record book Unity paints beside it. The page owns no gameplay: the
/// flow starts the leg, owns its clock and prices both boards' rows, and the
/// page only paints what it is handed, exactly like the valuation page.
///
/// The page stays up until the player leaves it (IKeepPanelOpen): a player
/// reading a board must not lose it to the 15 s idle hide.
/// </summary>
public sealed class RoadTestScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly Action _onAccel;
    private readonly Action _onBrake;
    private readonly Action _onPhoto;
    private readonly Action _onContinue;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;

    private bool _results;
    private bool _boardWaiting;
    private string _resultText = string.Empty;
    private IReadOnlyList<RoadTestRow> _rows = [];
    // The all-cars record book beside the car's own board: every tested car's
    // best, the car under test highlighted.
    private IReadOnlyList<RoadTestRow> _allCarsRows = [];
    // Which board the result view paints: the accel time or the brake distance.
    private RoadTestMetric _metric = RoadTestMetric.AccelerationTime;
    private bool _accelDone;
    private bool _brakeDone;
    private bool _accelFlash;
    private bool _brakeFlash;
    private bool _photoFlash;
    private bool _continueFlash;
    private bool _backFlash;

    public RoadTestScreenViewModel(
        CarSpec car, bool drivesThePick,
        Action onAccel, Action onBrake, Action onPhoto,
        Action onContinue, ICommand backCommand, VoiceBeat beat)
    {
        ArgumentNullException.ThrowIfNull(car);
        ArgumentNullException.ThrowIfNull(onAccel);
        ArgumentNullException.ThrowIfNull(onBrake);
        ArgumentNullException.ThrowIfNull(onPhoto);
        ArgumentNullException.ThrowIfNull(onContinue);
        ArgumentNullException.ThrowIfNull(backCommand);
        ArgumentNullException.ThrowIfNull(beat);
        _onAccel = onAccel;
        _onBrake = onBrake;
        _onPhoto = onPhoto;
        _onContinue = onContinue;
        _backCommand = backCommand;
        _beat = beat;
        CarName = CarTitles.Of(car);
        PickRequiredText = Strings.Format("RoadTestPickRequired", CarName);
        _drivesThePick = drivesThePick;
        AccelCommand = new RelayCommand(Accel, () => CanAccel);
        BrakeCommand = new RelayCommand(Brake, () => CanBrake);
        PhotoCommand = new RelayCommand(Photo, () => CanPhoto);
        ContinueCommand = new RelayCommand(Continue, () => IsResults);
        BackCommand = new RelayCommand(Back);
        RebuildVoice();
    }

    /// <summary>Unity's screen title ("ROAD TEST").</summary>
    public string Title { get; } = Strings.Get("RoadTestTitle");

    /// <summary>
    /// The week's car under the title, all caps like every car title: Unity's
    /// screen names the car being tested, and the page has no other use for it.
    /// </summary>
    public string CarName { get; }

    /// <summary>Unity's 0-60 button.</summary>
    public string AccelLabel { get; } = Strings.Get("RoadTestStart");

    /// <summary>The brake leg's button ("100-0").</summary>
    public string BrakeLabel { get; } = Strings.Get("RoadTestBrakeStart");

    /// <summary>The photo leg's button, revealed after both tests.</summary>
    public string PhotoLabel { get; } = Strings.Get("RoadTestPhoto");

    /// <summary>The brief under the 0-60 button.</summary>
    public string AccelText { get; } = Strings.Get("RoadTestAccelIntro");

    /// <summary>The brief under the 100-0 button.</summary>
    public string BrakeText { get; } = Strings.Get("RoadTestBrakeIntro");

    /// <summary>
    /// The brief under the photo button (Unity's photo op: any location on the
    /// map, so the shoot differs from the last one).
    /// </summary>
    public string PhotoText { get; } = Strings.Get("RoadTestPhotoIntro");

    /// <summary>
    /// True while the car at the door is this week's pick. Both legs can only
    /// run in that car: the board rows are the pick's, so a run in any other car
    /// would set one driver's time on another car's record. The flow keeps it
    /// live while the page is up, so a car swapped at the door flips the page
    /// without the player having to leave and come back.
    /// </summary>
    public bool DrivesThePick
    {
        get => _drivesThePick;
        private set
        {
            if (SetField(ref _drivesThePick, value))
            {
                OnPropertyChanged(nameof(ShowPickRequired));
                OnPropertyChanged(nameof(ShowBack));
            }
        }
    }
    private bool _drivesThePick;

    /// <summary>True while the page has to ask the player to enter the pick's car.</summary>
    public bool ShowPickRequired => !DrivesThePick;

    /// <summary>
    /// The page's way out, offered on the test-centre view only while the pick
    /// is at the door. WHY hidden while the pick is required: BACK abandons the
    /// visit for good, and a player fetching the right car has to find the
    /// test centre still open when they come back — leaving would cost them
    /// the whole drive. Once the pick arrives the page is a normal entry again.
    /// </summary>
    public bool ShowBack => IsReady && DrivesThePick;

    /// <summary>"This week's test is for the X. Enter that car."</summary>
    public string PickRequiredText { get; }

    /// <summary>The page's way out before a run.</summary>
    public string BackLabel { get; } = Strings.Get("RoadTestBack");

    /// <summary>The page's way out after a run.</summary>
    public string ContinueLabel { get; } = Strings.Get("RoadTestContinue");

    /// <summary>Unity's "GLOBAL LEADERBOARD" caption.</summary>
    public string BoardTitle { get; } = Strings.Get("RoadTestBoard");

    /// <summary>The all-cars record book's caption, under the car's own board.</summary>
    public string AllCarsTitle { get; } = Strings.Get("RoadTestAllCars");

    /// <summary>Shown while the board read is in flight.</summary>
    public string WaitingText { get; } = Strings.Get("RoadTestWaiting");

    /// <summary>False once a run landed: the test buttons and the briefs are done.</summary>
    public bool IsReady => !_results;

    /// <summary>True once the finish sentence is up.</summary>
    public bool IsResults => _results;

    /// <summary>0-60 is live only on the test-centre view AND in the pick's car.</summary>
    public bool CanAccel => IsReady && DrivesThePick && !_accelDone;

    /// <summary>The brake leg follows the same rule, once per visit.</summary>
    public bool CanBrake => IsReady && DrivesThePick && !_brakeDone;

    /// <summary>Unity's rule: the photo button appears after both legs, still open until taken.</summary>
    public bool ShowPhoto => CanPhoto;

    /// <summary>True while the photo button is offered: both legs taken, in the pick.</summary>
    public bool CanPhoto => IsReady && DrivesThePick && _accelDone && _brakeDone;

    /// <summary>The finish sentence ("You went from 0 to 60 km/h in 5.432 s ...").</summary>
    public string ResultText
    {
        get => _resultText;
        private set => SetField(ref _resultText, value);
    }

    /// <summary>The board rows, own row included; empty before the first run.</summary>
    public IReadOnlyList<RoadTestRow> Rows
    {
        get => _rows;
        private set
        {
            if (SetField(ref _rows, value))
            {
                OnPropertyChanged(nameof(HasBoard));
            }
        }
    }

    /// <summary>True while the board has a row to paint.</summary>
    public bool HasBoard => Rows.Count > 0;

    /// <summary>
    /// The all-cars record book's rows: every tested car, one row each, the car
    /// under test highlighted (Unity's overall standings beside the car's board).
    /// </summary>
    public IReadOnlyList<RoadTestRow> AllCarsRows
    {
        get => _allCarsRows;
        private set
        {
            if (SetField(ref _allCarsRows, value))
            {
                OnPropertyChanged(nameof(HasAllCarsBoard));
            }
        }
    }

    /// <summary>True while the all-cars book has a row to paint.</summary>
    public bool HasAllCarsBoard => AllCarsRows.Count > 0;

    /// <summary>True while the board read is still out; the local row is up meanwhile.</summary>
    public bool IsWaiting => _boardWaiting;

    /// <summary>Starts the 0-60 leg; the flow takes the panel down to let the player drive.</summary>
    public ICommand AccelCommand { get; }

    /// <summary>Starts the 100-0 leg; the flow takes the panel down to let the player drive.</summary>
    public ICommand BrakeCommand { get; }

    /// <summary>The photo mission, offered once both legs ran.</summary>
    public ICommand PhotoCommand { get; }

    /// <summary>Leaves the result view: the flow hands the test centre back.</summary>
    public ICommand ContinueCommand { get; }

    /// <summary>Back to the control page, no result.</summary>
    public ICommand BackCommand { get; }

    /// <summary>Teal-glow acknowledgment for the 0-60 voice tap.</summary>
    public bool AccelFlash
    {
        get => _accelFlash;
        private set => SetField(ref _accelFlash, value);
    }

    /// <summary>Teal-glow acknowledgment for the 100-0 voice tap.</summary>
    public bool BrakeFlash
    {
        get => _brakeFlash;
        private set => SetField(ref _brakeFlash, value);
    }

    /// <summary>Teal-glow acknowledgment for the PHOTO MISSION voice tap.</summary>
    public bool PhotoFlash
    {
        get => _photoFlash;
        private set => SetField(ref _photoFlash, value);
    }

    /// <summary>Teal-glow acknowledgment for the CONTINUE voice tap.</summary>
    public bool ContinueFlash
    {
        get => _continueFlash;
        private set => SetField(ref _continueFlash, value);
    }

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash
    {
        get => _backFlash;
        private set => SetField(ref _backFlash, value);
    }

    /// <summary>Spoken labels the page answers to.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>
    /// The flow reports the finished run: the page flips to its result view and
    /// shows the run the player just drove, in the board's own unit (seconds or
    /// metres), beside the car's board row and the all-cars book's row for it.
    /// The leg is banked as taken for this visit, which is what reveals the
    /// photo mission once both are done. The live board rows arrive separately,
    /// so an unreachable board never blanks the page.
    /// </summary>
    public void ShowResult(
        string resultLine, IReadOnlyList<RoadTestStanding> standings,
        IReadOnlyList<RoadTestCarStanding> allCars, RoadTestMetric metric)
    {
        _metric = metric;
        if (metric == RoadTestMetric.BrakeDistance)
        {
            _brakeDone = true;
        }
        else
        {
            _accelDone = true;
        }
        _results = true;
        ResultText = resultLine;
        _boardWaiting = true;
        Rows = BuildRows(standings);
        AllCarsRows = BuildAllCarsRows(allCars);
        RaisePhaseChanged();
    }

    /// <summary>The board finished loading: the rows replace the local one.</summary>
    public void ShowBoard(IReadOnlyList<RoadTestStanding> standings)
    {
        _boardWaiting = false;
        Rows = BuildRows(standings);
        OnPropertyChanged(nameof(IsWaiting));
    }

    /// <summary>The all-cars book finished loading: the rows replace the local one.</summary>
    public void ShowAllCarsBoard(IReadOnlyList<RoadTestCarStanding> standings) =>
        AllCarsRows = BuildAllCarsRows(standings);

    /// <summary>
    /// CONTINUE from a result: the page goes back to the test centre, with the
    /// leg it just drove banked and the other leg (or the photo mission) now
    /// on offer. The visit stays open; BACK is the way out.
    /// </summary>
    public void ShowTestCentre()
    {
        _results = false;
        _boardWaiting = false;
        ResultText = string.Empty;
        Rows = [];
        AllCarsRows = [];
        RaisePhaseChanged();
    }

    /// <summary>
    /// The car driven at the door is not this week's pick (or stopped being it
    /// mid-run): the page goes back to its test-centre view with the "enter
    /// that car" line and the legs re-armed for when the player does.
    /// </summary>
    public void MarkPickRequired()
    {
        if (!DrivesThePick)
        {
            return;
        }

        DrivesThePick = false;
        RaisePhaseChanged();
    }

    /// <summary>
    /// The pick is now the car at the door (the player swapped to it while the
    /// page was waiting): the instruction and the legs come back, the way out
    /// with them, so the run the player came for can start without leaving the
    /// test centre.
    /// </summary>
    public void MarkPickArrived()
    {
        if (DrivesThePick)
        {
            return;
        }

        DrivesThePick = true;
        RaisePhaseChanged();
    }

    private void Accel()
    {
        if (!CanAccel)
        {
            return;
        }
        AccelFlash = false;
        _beat.Cancel();
        _onAccel();
    }

    private void Brake()
    {
        if (!CanBrake)
        {
            return;
        }
        BrakeFlash = false;
        _beat.Cancel();
        _onBrake();
    }

    private void Photo()
    {
        if (!CanPhoto)
        {
            return;
        }
        PhotoFlash = false;
        _beat.Cancel();
        _onPhoto();
    }

    private void Continue()
    {
        if (!IsResults)
        {
            return;
        }
        ContinueFlash = false;
        _onContinue();
    }

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out.
    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }

    private void FlashThenAccel()
    {
        AccelFlash = true;
        _beat.Defer(Accel);
    }

    private void FlashThenBrake()
    {
        BrakeFlash = true;
        _beat.Defer(Brake);
    }

    private void FlashThenPhoto()
    {
        PhotoFlash = true;
        _beat.Defer(Photo);
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

    // Unity's board painting: the top slice, then — when the player's run fell
    // outside it — a separator and the own row, highlighted. The row's reading
    // is the board's own unit: seconds for 0-60, metres for 100-0.
    private List<RoadTestRow> BuildRows(IReadOnlyList<RoadTestStanding> standings)
    {
        string rowKey = RowKey();
        var rows = new List<RoadTestRow>(standings.Count + 1);
        for (int i = 0; i < standings.Count; i++)
        {
            RoadTestStanding standing = standings[i];
            if (standing.IsOwn && standing.Position != i + 1)
            {
                rows.Add(RoadTestRow.CreateSeparator());
            }
            rows.Add(new RoadTestRow(
                standing.Position,
                standing.PlayerName,
                Strings.Format(rowKey, standing.Value),
                standing.IsOwn));
        }
        return rows;
    }

    // The all-cars book paints one row per car in the same unit. WHY no
    // separator: every tested car is in this list, so its row is never outside
    // the slice — the tested car is simply highlighted where it stands.
    private List<RoadTestRow> BuildAllCarsRows(
        IReadOnlyList<RoadTestCarStanding> standings)
    {
        string rowKey = RowKey();
        var rows = new List<RoadTestRow>(standings.Count);
        for (int i = 0; i < standings.Count; i++)
        {
            RoadTestCarStanding standing = standings[i];
            rows.Add(new RoadTestRow(
                standing.Position,
                standing.CarName,
                Strings.Format(rowKey, standing.Value),
                standing.IsTestedCar));
        }
        return rows;
    }

    private string RowKey() => _metric == RoadTestMetric.BrakeDistance
        ? "RoadTestRowMetres"
        : "RoadTestRowTime";

    private void RaisePhaseChanged()
    {
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsResults));
        OnPropertyChanged(nameof(IsWaiting));
        OnPropertyChanged(nameof(CanAccel));
        OnPropertyChanged(nameof(CanBrake));
        OnPropertyChanged(nameof(CanPhoto));
        OnPropertyChanged(nameof(ShowPhoto));
        OnPropertyChanged(nameof(ShowBack));
        ((RelayCommand)AccelCommand).RaiseCanExecuteChanged();
        ((RelayCommand)BrakeCommand).RaiseCanExecuteChanged();
        ((RelayCommand)PhotoCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ContinueCommand).RaiseCanExecuteChanged();
        RebuildVoice();
    }

    // A leg's word only answers while that leg can actually run: a mic that
    // heard it in the wrong car (or after the leg was taken) would start
    // nothing and say nothing. BACK follows the way out the page offers, and
    // the photo word waits for Unity's both-legs rule.
    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>();
        if (!IsReady)
        {
            actions[ContinueLabel] = FlashThenContinue;
        }
        else
        {
            if (CanAccel)
            {
                actions[AccelLabel] = FlashThenAccel;
            }
            if (CanBrake)
            {
                actions[BrakeLabel] = FlashThenBrake;
            }
            if (ShowPhoto)
            {
                actions[PhotoLabel] = FlashThenPhoto;
            }
            if (ShowBack)
            {
                actions[BackLabel] = FlashThenBack;
            }
        }
        VoiceActions = actions;
    }
}
