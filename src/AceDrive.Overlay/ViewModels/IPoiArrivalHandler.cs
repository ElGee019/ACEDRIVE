using System.Windows.Input;
using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One POI family's arrival behaviour (PORTPLAN item 24, Unity
/// IPoiTypeHandler): decide whether it owns a POI and build the screen the
/// player enters. The host owns navigation, so the handler never swaps
/// screens itself. A new business is a new implementation plus one router
/// registration, not a new branch in MainViewModel.
/// </summary>
public interface IPoiArrivalHandler
{
    /// <summary>True when this handler owns the POI's type.</summary>
    bool Owns(PointOfInterest poi);

    /// <summary>
    /// The screen to show, or null when the interior cannot be built. The
    /// host supplies the BACK command and the state-changed save callback.
    /// </summary>
    object? Open(PointOfInterest poi, ICommand backCommand, Action onChanged);
}
