namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Navigation seam: screens never open each other directly, they call these
/// flow verbs and the controller swaps the visible screen. Companion parity:
/// the brand page is the onboarding home every wake lands on, until the jobs
/// screen (Max's grocery, PORTPLAN item 5) replaces it as the terminal state.
/// </summary>
public interface IScreenController
{
    void ChooseBrand(string brand);
    void ChooseCar(string carId);
    void FinishDialogue();
}
