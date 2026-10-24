namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The panel and the page it shows: whether the overlay is visible, which
/// screen is up, and when the player last touched it. The view binds the first
/// two, the idle countdown reads the stamp, and the collaborators that only
/// need to swap pages can take this instead of the whole shell.
///
/// It owns no policy: when to reveal, which page to open and what a call does
/// mid-job stay in the shell that calls it.
/// </summary>
public sealed class OverlayShell : ViewModelBase
{
    private object? _screen;
    private bool _shown;

    /// <summary>Active screen: an onboarding page, a jobs page or null.</summary>
    public object? CurrentScreen
    {
        get => _screen;
        private set
        {
            if (SetField(ref _screen, value))
            {
                ScreenChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>True while the overlay panel is visible.</summary>
    public bool IsShown
    {
        get => _shown;
        private set => SetField(ref _shown, value);
    }

    /// <summary>
    /// When the player last touched the overlay. The idle countdown measures
    /// against it, so it is a stamp rather than a flag.
    /// </summary>
    public DateTime LastActivity { get; private set; }

    /// <summary>Raised after the page changed, for the state derived from it.</summary>
    public event EventHandler? ScreenChanged;

    /// <summary>Swaps the visible page without touching visibility.</summary>
    public void ShowScreen(object? screen) => CurrentScreen = screen;

    /// <summary>Reveals the panel on the given page (entering a shop).</summary>
    public void Reveal(object screen)
    {
        CurrentScreen = screen;
        IsShown = true;
    }

    public void SetShown(bool shown) => IsShown = shown;

    public void RecordActivity(DateTime now) => LastActivity = now;
}
