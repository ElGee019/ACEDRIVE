using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The slice of the shell the POI world reports to: a door that connected
/// re-reads the board page, the dealer's weekly floor follows the calendar
/// week, and a purchase opens its follow-up choice. The world owns the doors,
/// so the shell only reports; nothing here answers a question about the world.
/// </summary>
public interface IPoiWorldHost
{
    /// <summary>Re-reads the cached board page after a door or store connected.</summary>
    void RefreshBoard();

    /// <summary>
    /// The calendar week the dealer's floor is drawn from. It stays with the
    /// coordinator until <c>DayCycle</c> owns it (S5), so it arrives as a
    /// report instead of the world reaching for the shell.
    /// </summary>
    int DisplayWeek { get; }

    /// <summary>Banks a dealer purchase and shows the new-car choice.</summary>
    void CarBought(CarSpec car);
}
