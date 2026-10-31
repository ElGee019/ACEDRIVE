using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The market valuation's briefing page (PORTPLAN 49): the calendar's Monday row
/// opens it before the companion pins the venue, so the player reads the week's
/// terms — what the jury compares, what it scores and how long an entry stands —
/// before a drive that cannot be cancelled starts. START DRIVE hands the flow its
/// drive, BACK hands the panel back to the calendar; the page paints copy and
/// decides nothing.
///
/// It keeps the panel open (<see cref="IKeepPanelOpen"/>): three paragraphs take
/// longer than the 15 s idle window, and a reader hidden mid-page would have to
/// re-tap the row to see the terms again. START hides the panel anyway, so the
/// opt-out never holds it over the drive.
/// </summary>
public sealed class ValuationBriefingScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly Action _onStart;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;

    public ValuationBriefingScreenViewModel(
        string venueLine, Action onStart, ICommand backCommand, VoiceBeat beat)
    {
        ArgumentNullException.ThrowIfNull(venueLine);
        ArgumentNullException.ThrowIfNull(onStart);
        ArgumentNullException.ThrowIfNull(backCommand);
        ArgumentNullException.ThrowIfNull(beat);

        VenueLine = venueLine;
        _onStart = onStart;
        _backCommand = backCommand;
        _beat = beat;
        StartCommand = new RelayCommand(Start);
        BackCommand = new RelayCommand(Back);
        RebuildVoice();
    }

    /// <summary>Unity's screen title; the briefing is the same show.</summary>
    public string Title { get; } = Strings.Get("ValuationTitle");

    /// <summary>"This week's venue: {address}", so the drive is known up front.</summary>
    public string VenueLine { get; }

    /// <summary>
    /// What the week asks and what the jury scores: the pick, the compared car,
    /// the six terms and the entry's two-day stand.
    /// </summary>
    public string IntroLine { get; } = Strings.Get("ValuationBriefIntro");

    /// <summary>The page's one forward verb: pins the venue and starts the drive.</summary>
    public ICommand StartCommand { get; }

    /// <summary>Leaves before the drive started; the flow shows the calendar.</summary>
    public ICommand BackCommand { get; }

    /// <summary>START DRIVE's label, spoken as well as painted.</summary>
    public string StartLabel { get; } = Strings.Get("ValuationBriefStart");

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
            [Strings.Get("ValuationBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
    }
}
