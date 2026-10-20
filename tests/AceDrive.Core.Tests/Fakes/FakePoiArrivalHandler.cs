using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Records router dispatch without building a real interior.</summary>
internal sealed class FakePoiArrivalHandler : IPoiArrivalHandler
{
    public FakePoiArrivalHandler(string type, object? screen = null)
    {
        Type = type;
        Screen = screen;
    }

    public string Type { get; }

    public object? Screen { get; }

    public int Opens { get; private set; }

    public bool Owns(PointOfInterest poi) =>
        string.Equals(poi.Type, Type, StringComparison.OrdinalIgnoreCase);

    public object? Open(PointOfInterest poi, ICommand backCommand, Action onChanged)
    {
        Opens++;
        return Screen;
    }
}
