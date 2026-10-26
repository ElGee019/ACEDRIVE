namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// A page that owns a wall-clock phase advances on the one 10 Hz loop: the
/// coordinator ticks the active page, the same way the taxi flow ticks its own
/// request page. The Cento Miglia build countdown is the first such page; a new
/// one opts in by implementing this instead of growing the coordinator's type
/// checks.
/// </summary>
public interface IPolledScreen
{
    /// <summary>
    /// Advances the page to <paramref name="now"/>. Called on every poll, so it
    /// must repaint on a changed second only and never throw.
    /// </summary>
    void Tick(DateTime now);
}
