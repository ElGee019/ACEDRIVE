using System.Windows.Input;
using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The provider of the <see cref="PoiTypes.Willy"/> markers the quest owns
/// (ToWhereIsWilly.md §3.3). The marker is a door, never a business: the
/// router only needs the one handler, and every other door keeps the handlers
/// it already had, so a wrong-address door and a shop still open the way they
/// always do.
/// </summary>
public sealed class WhereIsWillyPoiHandler : IPoiArrivalHandler
{
    private readonly WhereIsWillyFlow _flow;

    public WhereIsWillyPoiHandler(WhereIsWillyFlow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);
        _flow = flow;
    }

    public bool Owns(PointOfInterest poi) =>
        string.Equals(poi.Type, PoiTypes.Willy, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// ENTER on a quest marker: the home door pays and returns the receipt
    /// page, the wrong-address door clears the run. Null means the door has
    /// nothing to show, which is the wrong-address path's whole point.
    /// </summary>
    public object? Open(PointOfInterest poi, ICommand backCommand, Action onChanged) =>
        _flow.Enter(poi);
}
