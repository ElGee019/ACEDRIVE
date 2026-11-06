using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Friday's pancake briefing (PORTPLAN item 52): the calendar row opens it
/// before the companion pins grandma's door, so the player reads what the visit
/// gives (a plate off the meter and a fresh stack for the road) and where it is.
/// START DRIVE hands the flow its leg, BACK hands the panel back to the
/// calendar; the page paints copy and decides nothing.
///
/// It keeps the panel open (<see cref="IKeepPanelOpen"/>): the copy takes longer
/// than the 15 s idle window, and START hides the panel anyway, so the opt-out
/// never holds it over the drive.
/// </summary>
public sealed class PancakeBriefingScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly Action _onStart;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;

    public PancakeBriefingScreenViewModel(
        string venueLine, string hungerLine, bool hungerAmber,
        Action onStart, ICommand backCommand, VoiceBeat beat)
    {
        ArgumentNullException.ThrowIfNull(venueLine);
        ArgumentNullException.ThrowIfNull(hungerLine);
        ArgumentNullException.ThrowIfNull(onStart);
        ArgumentNullException.ThrowIfNull(backCommand);
        ArgumentNullException.ThrowIfNull(beat);

        VenueLine = venueLine;
        HungerLine = hungerLine;
        HungerAmber = hungerAmber;
        _onStart = onStart;
        _backCommand = backCommand;
        _beat = beat;
        StartCommand = new RelayCommand(Start);
        BackCommand = new RelayCommand(Back);
        RebuildVoice();
    }

    /// <summary>"PANCAKES".</summary>
    public string Title { get; } = Strings.Get("PancakeTitle");

    /// <summary>"Grandma's house: {address}", so the drive is known up front.</summary>
    public string VenueLine { get; }

    /// <summary>What the visit gives: the plate and the stack for the road.</summary>
    public string IntroLine { get; } = Strings.Get("PancakeBriefingBody");

    /// <summary>The reading the plate will come off, printed before the drive.</summary>
    public string HungerLine { get; }

    /// <summary>True inside Unity's amber hunger band (the label's orange).</summary>
    public bool HungerAmber { get; }

    /// <summary>START DRIVE's label, spoken as well as painted.</summary>
    public string StartLabel { get; } = Strings.Get("PancakeStartDrive");

    /// <summary>The page's one forward verb: pins grandma's house and drives there.</summary>
    public ICommand StartCommand { get; }

    /// <summary>Leaves before the drive started; the flow shows the calendar.</summary>
    public ICommand BackCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating START tap.</summary>
    public bool StartFlash { get => _startFlash; private set => SetField(ref _startFlash, value); }
    private bool _startFlash;

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    /// <summary>START DRIVE and BACK answer their labels.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private void Start()
    {
        // A click is newer input than a pending voice tap: drop the deferred
        // verb so the flow cannot be asked to start the drive twice.
        StartFlash = false;
        BackFlash = false;
        _beat.Cancel();
        _onStart();
    }

    private void Back()
    {
        // The page's own navigation clears the glow too, so a revisit never
        // opens on a stale teal.
        StartFlash = false;
        BackFlash = false;
        _beat.Cancel();
        _backCommand.Execute(null);
    }

    private void RebuildVoice()
    {
        // Both verbs navigate: glow first, act at the end of the shared beat,
        // like every other briefing page's buttons.
        VoiceActions = new Dictionary<string, Action>
        {
            [StartLabel] = () =>
            {
                StartFlash = true;
                _beat.Defer(Start);
            },
            [Strings.Get("DeliveryBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
    }
}
