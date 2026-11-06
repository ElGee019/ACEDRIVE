using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The pancake visit's receipt (PORTPLAN item 52): the plate that came off the
/// meter, the stack grandma packed for the road and where it is kept. The flow
/// has already served the meal and stamped the week by the time this page is
/// built — the receipt only prints the numbers it was handed — and CONTINUE
/// closes it back onto the main page.
/// </summary>
public sealed class PancakeReceiptScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly ICommand _continueCommand;
    private readonly VoiceBeat _beat;

    public PancakeReceiptScreenViewModel(
        PancakeMeal meal, int maxHunger, ICommand continueCommand, VoiceBeat beat)
    {
        ArgumentNullException.ThrowIfNull(meal);
        ArgumentNullException.ThrowIfNull(continueCommand);
        ArgumentNullException.ThrowIfNull(beat);

        MealLine = Strings.Format("PancakeReceiptMeal", meal.Served, meal.After, maxHunger);
        _continueCommand = continueCommand;
        _beat = beat;
        ContinueCommand = new RelayCommand(Continue);
        RebuildVoice();
    }

    /// <summary>"PANCAKES!".</summary>
    public string Title { get; } = Strings.Get("PancakeReceiptTitle");

    /// <summary>"Straight off the griddle: hunger -14 (6/36)".</summary>
    public string MealLine { get; }

    /// <summary>Where the road stack went: the trunk, the port's in-car storage.</summary>
    public string PackedLine { get; } = Strings.Get("PancakeReceiptPacked");

    /// <summary>The page's one way out.</summary>
    public ICommand ContinueCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating CONTINUE tap.</summary>
    public bool ContinueFlash { get => _continueFlash; private set => SetField(ref _continueFlash, value); }
    private bool _continueFlash;

    /// <summary>The one verb answers its label.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private void Continue()
    {
        ContinueFlash = false;
        _beat.Cancel();
        _continueCommand.Execute(null);
    }

    private void RebuildVoice()
    {
        // CONTINUE navigates: glow first, act at the end of the shared beat, like
        // every other receipt's button.
        VoiceActions = new Dictionary<string, Action>
        {
            [Strings.Get("ReceiptContinue")] = () =>
            {
                ContinueFlash = true;
                _beat.Defer(Continue);
            },
        };
    }
}
