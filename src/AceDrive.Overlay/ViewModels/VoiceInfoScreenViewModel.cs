using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Voice-control page: first the Career Start insert after grandma's car,
/// later the ride-home explanation after the community centre grants the
/// taxi license. The flow owns the copy and what CONTINUE means, so one page
/// serves both lessons without a second view. The closing page carries only
/// its next-step line: the wake word and the hotkey now live on the menu,
/// above the clock, so the lesson is not repeated after it was taught.
/// </summary>
public sealed class VoiceInfoScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly VoiceBeat _beat;
    private readonly Action _onContinue;

    public VoiceInfoScreenViewModel(
        VoiceBeat beat, Action onContinue,
        string? leadText = null, string? detailText = null, string? hintText = null)
    {
        _beat = beat;
        _onContinue = onContinue;
        LeadText = leadText;
        DetailText = detailText;
        HintText = hintText;
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

    /// <summary>First explanation line: what the page teaches.</summary>
    public string? LeadText { get; }

    /// <summary>Second line: an example or the alternative hotkey.</summary>
    public string? DetailText { get; }

    /// <summary>Optional third line: the next step after the lesson.</summary>
    public string? HintText { get; }

    /// <summary>True only for the pages that carry a next-step line.</summary>
    public bool HasHint => !string.IsNullOrWhiteSpace(HintText);

    public ICommand ContinueCommand { get; }

    /// <summary>Spoken label -&gt; action, same Continue as the dialogue pages.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; }

    /// <summary>Teal-glow acknowledgment for a voice tap on CONTINUE.</summary>
    public bool ContinueFlash { get => _continueFlash; private set => SetField(ref _continueFlash, value); }
    private bool _continueFlash;

    private void Continue() => _onContinue();
}
