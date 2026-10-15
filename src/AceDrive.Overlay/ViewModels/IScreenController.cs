namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Navigation seam: screens never open each other directly, they call these
/// flow verbs and the controller swaps the visible screen. Companion parity:
/// every call to the app lands on the brand page and walks the introduction
/// down to the main control page (PORTPLAN item 54) — the terminal state until
/// the jobs screen (Max's grocery, PORTPLAN item 5) takes over.
/// </summary>
public interface IScreenController
{
    void ChooseBrand(string brand);
    void ChooseCar(string carId);
    void FinishDialogue();
}
