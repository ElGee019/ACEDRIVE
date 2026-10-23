namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The slice of the shell the introduction walk needs: swap the page, reveal
/// the panel on a POI interior or a story dialogue, open the menu the tour
/// hands over to, and bank the shared driver save when a purchase moves cash +
/// trunk together.
/// </summary>
public interface IStoryHost
{
    /// <summary>Active screen, or null; a POI interior answers a second ENTER.</summary>
    object? CurrentScreen { get; }

    /// <summary>True while the overlay panel is visible.</summary>
    bool IsShown { get; }

    /// <summary>Swaps the visible page.</summary>
    void ShowScreen(object? screen);

    /// <summary>Reveals the panel on the given page (entering a shop).</summary>
    void Present(object screen);

    /// <summary>Hides the panel.</summary>
    void Hide();

    /// <summary>The main control page (built once by the shell).</summary>
    object ControlScreen();

    /// <summary>The menu page the finished introduction lands on.</summary>
    object MenuScreen();

    /// <summary>Reveals the panel on the menu (the tour's final hand-over).</summary>
    void ShowMenu();

    /// <summary>Banks the shared driver file (wallet + trunk live in it).</summary>
    void SaveDriver();
}
