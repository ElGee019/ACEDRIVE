namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Narrow navigation seam for the voice-info page: it only needs to reach
/// the birthday dialogue. Kept separate from <see cref="IScreenController"/>
/// so a new screen does not grow that stable interface.
/// </summary>
public interface IVoiceInfoNavigator
{
    void ShowDialogue();
}
