using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The road test's briefing page (PORTPLAN 49): the calendar's Tuesday row opens
/// it before the companion pins La Strada, so the player reads what the week's
/// test is — the car under test, the two legs, the photo the magazine needs —
/// before a drive that cannot be cancelled starts. START DRIVE hands the flow
/// its drive, BACK hands the panel back to the calendar; the page paints copy and
/// decides nothing.
///
/// WHY the same shape as the market valuation's briefing: both rows start an
/// event that leaves the panel for a drive, so both explain the week before the
/// player commits. It keeps the panel open (<see cref="IKeepPanelOpen"/>)
/// because three blocks outlive the 15 s idle window; START hides the panel
/// anyway, so the opt-out never holds it over the drive.
/// </summary>
public sealed class RoadTestBriefingScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly Action _onStart;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;

    public RoadTestBriefingScreenViewModel(
        string carName, string venueLine, Action onStart, ICommand backCommand,
        VoiceBeat beat)
    {
        ArgumentNullException.ThrowIfNull(carName);
        ArgumentNullException.ThrowIfNull(venueLine);
        ArgumentNullException.ThrowIfNull(onStart);
        ArgumentNullException.ThrowIfNull(backCommand);
        ArgumentNullException.ThrowIfNull(beat);

        VenueLine = venueLine;
        IntroLine = Strings.Format("RoadTestBriefIntro", carName);
        _onStart = onStart;
        _backCommand = backCommand;
        _beat = beat;
        StartCommand = new RelayCommand(Start);
        BackCommand = new RelayCommand(Back);
        RebuildVoice();
    }

    /// <summary>Unity's screen title; the briefing is the same test.</summary>
    public string Title { get; } = Strings.Get("RoadTestTitle");

    /// <summary>"Test centre: La Strada, {address}", so the drive is known up front.</summary>
    public string VenueLine { get; }

    /// <summary>
    /// What the week asks: the cover car, the two runs that give it its specs,
    /// the reputation they buy and the crowd the issue sells to, so the visit is
    /// understood before it costs a drive.
    /// </summary>
    public string IntroLine { get; }

    /// <summary>The page's one forward verb: pins La Strada and starts the drive.</summary>
    public ICommand StartCommand { get; }

    /// <summary>Leaves before the drive started; the flow shows the jobs page.</summary>
    public ICommand BackCommand { get; }

    /// <summary>START DRIVE's label, spoken as well as painted.</summary>
    public string StartLabel { get; } = Strings.Get("RoadTestBriefStart");

    /// <summary>Teal-glow acknowledgment for the navigating START tap.</summary>
    public bool StartFlash
    {
        get => _startFlash;
        private set => SetField(ref _startFlash, value);
    }
    private bool _startFlash;

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash
    {
        get => _backFlash;
        private set => SetField(ref _backFlash, value);
    }
    private bool _backFlash;

    /// <summary>START DRIVE and BACK answer their labels.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private void Start()
    {
        // A click is newer input than a pending voice tap: drop the deferred
        // verb so the flow cannot be asked to start the drive twice.
        StartFlash = false;
        _beat.Cancel();
        _onStart();
    }

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out.
    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }

    private void RebuildVoice()
    {
        // Both verbs navigate: glow first, act at the end of the shared beat,
        // like every other page's buttons.
        VoiceActions = new Dictionary<string, Action>
        {
            [StartLabel] = () =>
            {
                StartFlash = true;
                _beat.Defer(Start);
            },
            [Strings.Get("RoadTestBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
    }
}
