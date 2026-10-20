using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The breakdown session (PORTPLAN item 39, port of Unity BreakdownPopup +
/// BreakdownFlow.BuildMessage/BuildTaskText): the interrupted trip's message,
/// the mechanic's tow fee and — when the engine failed its annual inspection —
/// the stranded hint that points at the mechanic page. The per-kind
/// consequences (job cut, cargo stripped, familiarity) are already applied by
/// the host before this page opens; the page only reports. Voice parity: BACK
/// navigates, so its tap glows teal first and waits out the shared beat.
/// </summary>
public sealed class BreakdownScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly VoiceBeat _beat;
    private readonly ICommand _backCommand;

    public BreakdownScreenViewModel(BreakdownOutcome outcome, VoiceBeat beat, ICommand backCommand)
    {
        _beat = beat;
        _backCommand = backCommand;
        BackCommand = new RelayCommand(Back);
        Message = Strings.Get(outcome.EngineFailure
            ? "BreakdownEngineFailure"
            : outcome.Kind switch
            {
                BreakdownKind.Taxi => "BreakdownTaxi",
                BreakdownKind.Delivery => "BreakdownDelivery",
                _ => "BreakdownFreeroam",
            });
        FailureVisible = outcome.EngineFailure;
        HintVisible = outcome.EngineFailure;
        FeeLine = Strings.Format("BreakdownFee", outcome.PickupFee);
        FeeVisible = outcome.PickupFee > 0.0;
        RebuildVoice();
    }

    public ICommand BackCommand { get; }

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>Unity's BuildMessage/BuildTaskText output for the kind.</summary>
    public string Message { get; }

    /// <summary>The engine-failure block, shown only for the stranded variant.</summary>
    public bool FailureVisible { get; }

    /// <summary>The mechanic hint, shown only for the stranded variant.</summary>
    public bool HintVisible { get; }

    /// <summary>Unity BreakdownFlow.PickupFee: the tow-in charge.</summary>
    public string FeeLine { get; }

    public bool FeeVisible { get; }

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out (bank-page precedent).
    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }

    // Back navigates: glow first, act at the end of the shared beat.
    private void RebuildVoice() =>
        VoiceActions = new Dictionary<string, Action>
        {
            [Strings.Get("DeliveryBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
}
