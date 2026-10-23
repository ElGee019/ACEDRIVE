using System.Windows.Input;
using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One business family's arrival on the POI router (PORTPLAN item 24). The
/// host supplies the page factory, so a new business is one registration
/// beside the others instead of a new branch in the entry path.
/// </summary>
public sealed class DelegatePoiHandler : IPoiArrivalHandler
{
    private readonly string _type;
    private readonly Func<PointOfInterest, ICommand, Action, object?> _open;

    public DelegatePoiHandler(
        string type, Func<PointOfInterest, ICommand, Action, object?> open)
    {
        _type = type;
        _open = open;
    }

    public bool Owns(PointOfInterest poi) =>
        string.Equals(poi.Type, _type, StringComparison.OrdinalIgnoreCase);

    public object? Open(PointOfInterest poi, ICommand backCommand, Action onChanged) =>
        _open(poi, backCommand, onChanged);
}
