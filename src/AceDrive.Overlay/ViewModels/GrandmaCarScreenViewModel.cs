using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Onboarding page 3: the car grandma hands over (PORTPLAN Career Start item
/// 3). One offer, drawn as a picture instead of a button — the pick is fixed,
/// so the page only has to ask the player to change into it and then confirm.
/// The page sits after the voice-consent answer, so its CONTINUE answers to
/// speech exactly like the birthday page's did: a teal acknowledgment, then
/// the hand-over at the end of the shared beat.
/// </summary>
public sealed class GrandmaCarScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly IScreenController _controller;
    private readonly VoiceBeat _beat;

    public GrandmaCarScreenViewModel(IScreenController controller, GrandmaCar car, VoiceBeat beat)
    {
        _controller = controller;
        _beat = beat;
        Car = car;
        ContinueCommand = new RelayCommand(ContinueNow);
        VoiceActions = new Dictionary<string, Action>(StringComparer.Ordinal)
        {
            [Strings.Get("DialogueContinue")] = ContinueByVoice,
        };
    }

    /// <summary>Grandma's offer: the one car the introduction asks for.</summary>
    public GrandmaCar Car { get; }

    /// <summary>
    /// False when the data ships no render for the offer; the page then shows
    /// the instruction line alone rather than an empty frame.
    /// </summary>
    public bool HasImage => Car.ImagePath is not null;

    public ICommand ContinueCommand { get; }

    /// <summary>Spoken label -&gt; action, the same CONTINUE the button runs.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; }

    /// <summary>Teal-glow acknowledgment for a voice tap on CONTINUE.</summary>
    public bool ContinueFlash { get => _continueFlash; private set => SetField(ref _continueFlash, value); }
    private bool _continueFlash;

    // A click is newer input: drop a pending voice tap and its glow so the
    // beat cannot run the hand-over a second time.
    private void ContinueNow()
    {
        _beat.Cancel();
        ContinueFlash = false;
        _controller.ChooseCar(Car.Id);
    }

    private void ContinueByVoice()
    {
        ContinueFlash = true;
        _beat.Defer(ContinueNow);
    }
}
