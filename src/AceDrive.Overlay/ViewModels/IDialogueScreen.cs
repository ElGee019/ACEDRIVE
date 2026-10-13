namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// A dialogue screen (birthday, quests, ...). The shell's idle auto-hide
/// stays suspended while a dialogue is still open, so a slow reader is
/// never hidden mid-conversation; it resumes once the dialogue reports
/// complete. New dialogue types opt in by implementing this.
/// </summary>
public interface IDialogueScreen
{
    /// <summary>True once the last page has been acknowledged.</summary>
    bool IsComplete { get; }
}
