using System.Windows.Input;
using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Dispatches a POI arrival to the first registered handler that owns its
/// type. The ordered list is the extension seam: later bank, dealer or
/// mechanic interiors join by adding a handler in the composition root.
/// </summary>
public sealed class PoiArrivalRouter
{
    private readonly IReadOnlyList<IPoiArrivalHandler> _handlers;

    public PoiArrivalRouter(IReadOnlyList<IPoiArrivalHandler> handlers) =>
        _handlers = handlers;

    /// <summary>
    /// The interior screen for this POI, or null when no handler claims it
    /// (the host then keeps the log-only fallback).
    /// </summary>
    public object? Open(PointOfInterest poi, ICommand backCommand, Action onChanged)
    {
        foreach (IPoiArrivalHandler handler in _handlers)
        {
            if (handler.Owns(poi))
            {
                return handler.Open(poi, backCommand, onChanged);
            }
        }
        return null;
    }
}
