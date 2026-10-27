using System.Windows.Input;
using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The provider of the <see cref="PoiTypes.Valuation"/> venue marker (PORTPLAN
/// item 49). The venue is a temporary door, never a business: parking only
/// raises the companion's ENTER button, and the tap (or its spoken word) runs
/// the arrival tail and hands back the showcase page.
/// </summary>
public sealed class ValuationPoiHandler : IPoiArrivalHandler
{
    private readonly ValuationFlow _flow;

    public ValuationPoiHandler(ValuationFlow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);
        _flow = flow;
    }

    public bool Owns(PointOfInterest poi) =>
        string.Equals(poi.Type, PoiTypes.Valuation, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// ENTER on this week's venue: consumes the venue and returns the showcase,
    /// or null when the leg is no longer live (a stale pin opens nothing).
    /// </summary>
    public object? Open(PointOfInterest poi, ICommand backCommand, Action onChanged) =>
        _flow.EnterVenue(poi);
}
