using System.Windows.Input;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The voice-control page that asks permission before the microphone opens.
/// CONFIRM lets the story flow start listening and continue the walk; NO THANK
/// YOU keeps the mic off and continues anyway. The page is deliberately
/// mouse-only (no <see cref="IVoiceScreen"/>): there is no open microphone to
/// answer it with, and consent must be an explicit click. The lesson about
/// using voice moves to the first birthday page, where the mic is live and its
/// CONTINUE is the button the example names.
/// </summary>
public sealed class VoiceConsentScreenViewModel : ViewModelBase
{
    public VoiceConsentScreenViewModel(Action onConfirm, Action onDecline)
    {
        ConfirmCommand = new RelayCommand(onConfirm);
        DeclineCommand = new RelayCommand(onDecline);
    }

    /// <summary>Allow voice control: the flow starts the mic and continues.</summary>
    public ICommand ConfirmCommand { get; }

    /// <summary>Keep the mic off and continue the introduction.</summary>
    public ICommand DeclineCommand { get; }
}
