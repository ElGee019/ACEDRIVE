using System.Windows.Input;
using AceDrive.Core;
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
    private readonly VoiceBeat _beat;

    public VoiceInfoScreenViewModel(IVoiceInfoNavigator navigator, VoiceBeat beat)
    {
        _navigator = navigator;
        _beat = beat;
        WakeText = Strings.Get("VoiceInfoWake");
        ButtonsText = Strings.Format("VoiceInfoButtons",
            Strings.Get("DialogueContinue").ToUpperInvariant());
        ContinueCommand = new RelayCommand(Continue);
        // Voice continue flashes teal first and the page waits out the
        // shared beat, so the glow shows before the flip. A click acts at
        // once with no glow.
        VoiceActions = new Dictionary<string, Action>
        {
            [Strings.Get("DialogueContinue")] = () =>
            {
                ContinueFlash = true;
                _beat.Defer(() =>
                {
                    ContinueFlash = false;
                    Continue();
                });
            },
        };
    }

    /// <summary>Wake-word explanation with a literal {BRAND} placeholder.</summary>
    public string WakeText { get; }

    /// <summary>Formatted button-by-voice line, example matches the real button label.</summary>
    public string ButtonsText { get; }

    public ICommand ContinueCommand { get; }

    /// <summary>Spoken label -&gt; action, same Continue as the dialogue pages.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; }

    /// <summary>Teal-glow acknowledgment for a voice tap on CONTINUE.</summary>
    public bool ContinueFlash { get => _continueFlash; private set => SetField(ref _continueFlash, value); }
    private bool _continueFlash;

    private void Continue() => _navigator.ShowDialogue();
}
