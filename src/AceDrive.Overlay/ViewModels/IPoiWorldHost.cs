using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The slice of the shell the POI world reports to: a door that connected
/// re-reads the board page, and a purchase opens its follow-up choice. The
/// world owns the doors, so the shell only reports; nothing here answers a
/// question about the world. (The calendar week the dealer's floor is drawn
/// from is read off <see cref="DayCycle"/>, which owns it.)
/// </summary>
public interface IPoiWorldHost
{
    /// <summary>Re-reads the cached board page after a door or store connected.</summary>
    void RefreshBoard();

    /// <summary>Banks a dealer purchase and shows the new-car choice.</summary>
    void CarBought(CarSpec car);
}
