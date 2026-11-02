using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The hunger explanation page: the first operating-zone expansion's CONTINUE
/// opens it before the page the receipt would have shown, so a new driver
/// learns that time makes them hungry, where food is sold and where it ends up.
/// TO TRUNK jumps straight to the trunk page the copy names; CONTINUE runs the
/// continuation. Same skeleton as the other explanation pages: title band,
/// body centered in the panel, the two verbs stacked at the bottom. The page
/// only ever opens once per career, so its copy owns no "seen" flag.
///
/// It keeps the panel open (<see cref="IKeepPanelOpen"/>): the body outlives
/// the 15 s idle window, and a reader hidden mid-page would lose the
/// explanation for good — the first expansion never comes again.
/// </summary>
public sealed class HungerIntroScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly Action _onContinue;
    private readonly ICommand _toTrunkCommand;
    private readonly VoiceBeat _beat;

    public HungerIntroScreenViewModel(
        Action onContinue, ICommand toTrunkCommand, VoiceBeat beat)
    {
        ArgumentNullException.ThrowIfNull(onContinue);
        ArgumentNullException.ThrowIfNull(toTrunkCommand);
        ArgumentNullException.ThrowIfNull(beat);

        _onContinue = onContinue;
        _toTrunkCommand = toTrunkCommand;
        _beat = beat;
        Title = Strings.Get("HungerIntroTitle");
        Body = Strings.Get("HungerIntroBody");
        ContinueLabel = Strings.Get("HungerIntroContinue");
        ToTrunkLabel = Strings.Get("HungerIntroToTrunk");
        ContinueCommand = new RelayCommand(Continue);
        ToTrunkCommand = new RelayCommand(ToTrunk);
        RebuildVoice();
    }

    /// <summary>"GETTING HUNGRY".</summary>
    public string Title { get; }

    /// <summary>What hunger is, where food is bought and how the trunk is reached.</summary>
    public string Body { get; }

    /// <summary>CONTINUE's label, spoken as well as painted.</summary>
    public string ContinueLabel { get; }

    /// <summary>TO TRUNK's label, spoken as well as painted.</summary>
    public string ToTrunkLabel { get; }

    /// <summary>Hands the player to the page the receipt would have opened.</summary>
    public ICommand ContinueCommand { get; }

    /// <summary>Opens the trunk page directly.</summary>
    public ICommand ToTrunkCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating CONTINUE tap.</summary>
    public bool ContinueFlash
    {
        get => _continueFlash;
        private set => SetField(ref _continueFlash, value);
    }
    private bool _continueFlash;

    /// <summary>Teal-glow acknowledgment for the navigating TO TRUNK tap.</summary>
    public bool ToTrunkFlash
    {
        get => _toTrunkFlash;
        private set => SetField(ref _toTrunkFlash, value);
    }
    private bool _toTrunkFlash;

    /// <summary>Both buttons answer their labels.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private void Continue()
    {
        // A click is newer input than a pending voice tap: drop the deferred
        // verb so the continuation cannot be asked to run twice.
        ContinueFlash = false;
        ToTrunkFlash = false;
        _beat.Cancel();
        _onContinue();
    }

    private void ToTrunk()
    {
        // The trunk page's own navigation cancels the beat too; clearing the
        // flashes here keeps a stale glow off the page if it is ever revisited.
        ContinueFlash = false;
        ToTrunkFlash = false;
        _beat.Cancel();
        _toTrunkCommand.Execute(null);
    }

    private void RebuildVoice()
    {
        // Both verbs navigate: glow first, act at the end of the shared beat,
        // like every other page's buttons.
        VoiceActions = new Dictionary<string, Action>
        {
            [ContinueLabel] = () =>
            {
                ContinueFlash = true;
                _beat.Defer(Continue);
            },
            [ToTrunkLabel] = () =>
            {
                ToTrunkFlash = true;
                _beat.Defer(ToTrunk);
            },
        };
    }
}
