// Manual destination pins (PORTPLAN item 46, Unity
// PlayerData.manuallySetDestinations): the addresses page's pin is the
// overlay's "set a destination yourself" action, so its count is what the
// ToDo subject reads. Persisted through the driver save.
namespace AceDrive.Core;

/// <summary>How many destinations the player pinned from the addresses page.</summary>
public sealed class DestinationPins
{
    public DestinationPins(DriverState? saved = null) =>
        Count = Math.Max(0, saved?.ManuallySetDestinations ?? 0);

    public int Count { get; private set; }

    /// <summary>One pin; returns the new lifetime count.</summary>
    public int Register()
    {
        Count++;
        return Count;
    }
}
