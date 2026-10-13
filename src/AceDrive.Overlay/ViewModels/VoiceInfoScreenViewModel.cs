using System.Windows.Input;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Onboarding page 3: how voice control works (Career Start insert after
/// grandma's car). Explains app activation via the wake word plus
/// button-by-voice, then continues to the birthday dialogue.
/// </summary>
public sealed class VoiceInfoScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly IVoiceInfoNavigator _navigator;

    public VoiceInfoScreenViewModel(IVoiceInfoNavigator navigator)
    {
        _navigator = navigator;
        WakeText = Strings.Get("VoiceInfoWake");
        ButtonsText = Strings.Format("VoiceInfoButtons",
            Strings.Get("DialogueContinue").ToUpperInvariant());
        ContinueCommand = new RelayCommand(Continue);
        VoiceActions = new Dictionary<string, Action>
        {
            [Strings.Get("DialogueContinue")] = Continue,
        };
    }

    /// <summary>Wake-word explanation with a literal {BRAND} placeholder.</summary>
    public string WakeText { get; }

    /// <summary>Formatted button-by-voice line, example matches the real button label.</summary>
    public string ButtonsText { get; }

    public ICommand ContinueCommand { get; }

    /// <summary>Spoken label -&gt; action, same Continue as the dialogue pages.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; }

    private void Continue() => _navigator.ShowDialogue();
}
