using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The overlay panel's reveal policy: what a call opens, what revealing a POI,
/// a job page or an interrupting page does to the panel, how an interruption
/// hands the player back and what hiding means. The shell owns the state
/// (visible, page, activity stamp); this decides when that state moves.
///
/// WHY it is not the coordinator's: the story flow and the voice router both
/// reveal pages, and both are built by the root before the coordinator exists.
/// One owner keeps the first-show rule, the mid-job call gate and the log lines
/// in a single place instead of spreading them over three hosts.
/// </summary>
public sealed class PanelPresenter
{
    private readonly OverlayShell _shell;
    private readonly Navigation _nav;
    private readonly TaxiJobFlow _taxi;
    private readonly DeliveryFlow _delivery;
    private readonly TouristenfahrtenFlow _touristen;
    private readonly IClock _clock;
    private readonly ILog _log;

    // The page and visibility an interruption took away: one slot, rewritten
    // by the next interruption, so it needs no other invalidation — only the
    // interruption page's own CONTINUE reads it back. Restoring an arbitrary
    // page is safe because the idle rule hides the panel 15 s after the player
    // stops touching it, so the pages still up when a shift times out are the
    // ones that keep the panel open by design (a receipt, a POI, a dialogue).
    private object? _interruptedScreen;
    private bool _interruptedShown;

    public PanelPresenter(
        OverlayShell shell, Navigation navigation, TaxiJobFlow taxi,
        DeliveryFlow delivery, TouristenfahrtenFlow touristen,
        IClock clock, ILog log)
    {
        _shell = shell;
        _nav = navigation;
        _taxi = taxi;
        _delivery = delivery;
        _touristen = touristen;
        _clock = clock;
        _log = log;
    }

    /// <summary>True while the overlay panel is visible.</summary>
    public bool IsShown => _shell.IsShown;

    /// <summary>Active page, or null.</summary>
    public object? CurrentScreen => _shell.CurrentScreen;

    /// <summary>
    /// Raised after the panel left the screen. Hiding is a hard context break,
    /// so the voice router forgets the tap it last fired.
    /// </summary>
    public event EventHandler? Hidden;

    /// <summary>Counts as user presence for the idle auto-hide window.</summary>
    public void RecordActivity() => _shell.RecordActivity(_clock.UtcNow);

    /// <summary>
    /// The single "the app was called" seam: the Ctrl+Shift+C hotkey and the
    /// wake word both land here. WHY the job gate: from ACCEPT to the receipt
    /// the HUD owns the screen and the mic, so a call mid-job would let the
    /// player start a second job over the ride.
    /// </summary>
    public void Show()
    {
        if (JobInProgress)
        {
            _log.LogInfo("overlay call ignored: job in progress");
            return;
        }

        RecordActivity();
        if (_shell.IsShown)
        {
            return;
        }

        bool firstShow = !_shell.HasBeenShown;
        _shell.SetShown(true);
        // The first show of a hidden start reveals the page already built in
        // the constructor (the resumed walk page, or the menu past the
        // introduction); every later show opens the menu (Jobs leads to the
        // control page) instead of resuming whatever page was last open. It
        // rides the hidden -> shown edge only: a reveal while already visible
        // must not yank the player off the page they are using.
        if (firstShow && _shell.CurrentScreen is not null)
        {
            _log.LogInfo("overlay shown (first page)");
            return;
        }

        _nav.ShowMenuPage();
        _log.LogInfo("overlay shown (menu)");
    }

    /// <summary>
    /// Reveals the panel on a POI page (entering a shop or a story dialogue).
    /// The caller sets the page because Show() would open the menu instead.
    /// </summary>
    public void Present(object screen) => Reveal(screen, "overlay shown (POI)");

    /// <summary>
    /// Reveals the panel on a job page (refusal, breakdown, receipt). Same
    /// rule as <see cref="Present"/>, reported as job traffic.
    /// </summary>
    public void PresentJob(object screen) => Reveal(screen, "overlay shown (job)");

    /// <summary>
    /// Reveals a page that interrupts the player without them having started
    /// it (a shift that just ran out) and remembers the page and visibility it
    /// takes away, so <see cref="ResumeInterrupted"/> can hand them back. The
    /// capture happens before the swap, so it records the player's own screen,
    /// never this page.
    /// </summary>
    public void PresentInterruption(object screen)
    {
        _interruptedScreen = _shell.CurrentScreen;
        _interruptedShown = _shell.IsShown;
        Reveal(screen, "overlay shown (shift ended)");
    }

    /// <summary>
    /// The interruption page's way back: the player resumes the page the
    /// interruption took, and a panel that was hidden (the timeout caught them
    /// driving) stays hidden instead of opening a menu over the road. The tap
    /// is presence, so the idle countdown restarts with it — otherwise the
    /// restored page would be hidden again on the next poll.
    /// </summary>
    public void ResumeInterrupted()
    {
        if (!_interruptedShown)
        {
            Hide();
            return;
        }

        RecordActivity();
        if (_interruptedScreen is { } screen)
        {
            _nav.ShowScreen(screen);
            return;
        }

        // Nothing was up (the panel's very first page): the menu is the page
        // every later call opens, so that is where the player would have been.
        _nav.ShowMenuPage();
    }

    /// <summary>
    /// Shows the menu the finished introduction hands over to. It is the one
    /// page a call also opens, so the tour ends on the page every later call
    /// returns to.
    /// </summary>
    public void ShowMenu()
    {
        RecordActivity();
        _nav.ShowMenuPage();
        _shell.SetShown(true);
        _log.LogInfo("overlay shown (menu)");
    }

    /// <summary>Hides the panel; a hidden panel stays hidden.</summary>
    public void Hide()
    {
        if (!_shell.IsShown)
        {
            return;
        }

        _shell.SetShown(false);
        _log.LogInfo("overlay hidden");
        Hidden?.Invoke(this, EventArgs.Empty);
    }

    // The flows own their runs: a live taxi ride, delivery run or Touristenfahrten
    // run is work in progress, and the call gate reads the same definition the
    // idle-shift rule does.
    private bool JobInProgress =>
        _taxi.InProgress || _delivery.InProgress || _touristen.InProgress;

    private void Reveal(object screen, string logLine)
    {
        RecordActivity();
        _shell.Reveal(screen);
        _log.LogInfo(logLine);
    }
}
