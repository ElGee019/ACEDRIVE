namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Navigation seam: screens never open each other directly, they call these
/// flow verbs and the controller swaps the visible screen. Companion parity:
/// first paint lands on the opening screen, then the brand page walks the
/// introduction down to the main control page (PORTPLAN item 54); the tour's
/// hand-over lands on the menu page — the entry whose buttons take the player
/// to the control page and the job screens.
/// </summary>
public interface IScreenController
{
    void ChooseBrand(string brand);
    void ChooseCar(string carId);
}
