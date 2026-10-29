using System.Windows.Input;
using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// La Strada's arrival (PORTPLAN item 49). The magazine office is a story door
/// with no business interior, so this handler is its one way in: a begun road
/// test opens the test centre page, and every other visit keeps the ordinary
/// no-interior reading by answering null.
/// </summary>
public sealed class RoadTestPoiHandler : IPoiArrivalHandler
{
    private readonly RoadTestFlow _flow;

    public RoadTestPoiHandler(RoadTestFlow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);
        _flow = flow;
    }

    public bool Owns(PointOfInterest poi) =>
        string.Equals(poi.Id, StoryLaStrada.Definition.Id, StringComparison.Ordinal);

    /// <summary>
    /// ENTER at the door: the test centre page while a test is in flight.
    /// Null hands the door back to the router's normal handling.
    /// </summary>
    public object? Open(PointOfInterest poi, ICommand backCommand, Action onChanged) =>
        _flow.Enter(poi);
}
