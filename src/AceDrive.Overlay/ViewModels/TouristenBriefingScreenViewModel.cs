using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The Touristenfahrten run's briefing page: the calendar's Saturday row opens
/// it before the run draws its shop and corners, so the player reads what the
/// Saturday asks — load a shop's order, drive it out to the Nordschleife
/// corners, settle one receipt per stop — before a run that leaves the panel
/// starts. START draws and pins the run, BACK hands the panel back to the
/// calendar; the page paints copy and decides nothing.
///
/// WHY the same shape as the road test's and the market valuation's briefings:
/// all three rows start an event that takes the player off the panel, so all
/// three explain the event before the player commits. It keeps the panel open
/// (<see cref="IKeepPanelOpen"/>) because the explanation outlives the 15 s
/// idle window; START hides the panel anyway, so the opt-out never holds it
/// over the run.
/// </summary>
public sealed class TouristenBriefingScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly Action _onStart;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;

    public TouristenBriefingScreenViewModel(
        string title, Action onStart, ICommand backCommand, VoiceBeat beat)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(onStart);
        ArgumentNullException.ThrowIfNull(backCommand);
        ArgumentNullException.ThrowIfNull(beat);

        Title = title;
        _onStart = onStart;
        _backCommand = backCommand;
        _beat = beat;
        StartCommand = new RelayCommand(Start);
        BackCommand = new RelayCommand(Back);
        RebuildVoice();
    }

    /// <summary>
    /// The page's title: the real-world event running today when the calendar
    /// names one (an NLS round or the 24h meeting), otherwise the run's own
    /// weekend title. <see cref="TouristenCopy"/> resolves it; the page only
    /// paints what it is handed.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// What the Saturday asks: the shop order, the corner stops, the two HUD
    /// verbs and the per-dropoff receipts, so the run is understood before it
    /// hides the panel.
    /// </summary>
    public string IntroLine { get; } = Strings.Get("TouristenBriefIntro");

    /// <summary>The page's one forward verb: draws the run and pins the shop.</summary>
    public ICommand StartCommand { get; }

    /// <summary>Leaves before anything was drawn; the flow shows the calendar.</summary>
    public ICommand BackCommand { get; }

    /// <summary>START's label, spoken as well as painted.</summary>
    public string StartLabel { get; } = Strings.Get("TouristenBriefStart");

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

    /// <summary>START and BACK answer their labels.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private void Start()
    {
        // A click is newer input than a pending voice tap: drop the deferred
        // verb so the flow cannot be asked to draw the run twice.
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
            [Strings.Get("TouristenBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
    }
}
