using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The car-mod suggestion page: one question centred on the page, with
/// SUGGEST stacked directly above BACK in the bottom button band. The page
/// owns only navigation and the voice acknowledgment; what SUGGEST does with
/// a suggestion is the host's decision, because the submission path does not
/// exist yet. The host builds a fresh page on every visit like the other
/// You-menu doors.
/// </summary>
public sealed class SuggestCarModScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly ICommand _backCommand;
    private readonly ICommand _suggestCommand;
    private readonly VoiceBeat _beat;

    public SuggestCarModScreenViewModel(
        ICommand backCommand, ICommand suggestCommand, VoiceBeat beat)
    {
        _backCommand = backCommand;
        _suggestCommand = suggestCommand;
        _beat = beat;
        BackCommand = new RelayCommand(Back);
        SuggestCommand = new RelayCommand(Suggest);
        Prompt = Strings.Get("SuggestCarModPrompt");
        VoiceActions = new Dictionary<string, Action>
        {
            // Both buttons act through the shared voice beat, like the home
            // page: glow first, act at the end, clear the glow on the way out.
            [Strings.Get("SuggestCarModBack")] = () => FireVoice("back", _backCommand),
            [Strings.Get("SuggestCarModSubmit")] = () => FireVoice("suggest", _suggestCommand),
        };
    }

    /// <summary>The page's way back to the home page it was opened from.</summary>
    public ICommand BackCommand { get; }

    /// <summary>Reports the player's suggestion; the host owns the submission path.</summary>
    public ICommand SuggestCommand { get; }

    /// <summary>The question the page asks, centred under the top band.</summary>
    public string Prompt { get; }

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    /// <summary>Teal-glow acknowledgment for the SUGGEST tap.</summary>
    public bool SuggestFlash { get => _suggestFlash; private set => SetField(ref _suggestFlash, value); }
    private bool _suggestFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; }

    // A click is newer input than a pending voice tap: drop the deferred
    // navigation and the stale glow so the page cannot flip afterwards.
    private void Back()
    {
        _beat.Cancel();
        ClearFlash();
        _backCommand.Execute(null);
    }

    private void Suggest()
    {
        _beat.Cancel();
        ClearFlash();
        _suggestCommand.Execute(null);
    }

    private void FireVoice(string target, ICommand command)
    {
        BackFlash = target == "back";
        SuggestFlash = target == "suggest";
        _beat.Defer(() =>
        {
            ClearFlash();
            command.Execute(null);
        });
    }

    private void ClearFlash()
    {
        BackFlash = false;
        SuggestFlash = false;
    }
}
