using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>In-memory Touristenfahrten doors: one pickup plus a door per landmark.</summary>
internal sealed class FakeTouristenDoors : ITouristenDoors
{
    public PointOfInterest? PickupDoor { get; set; }

    public Dictionary<string, PointOfInterest> Doors { get; } = new(StringComparer.Ordinal);

    public int PickupCalls { get; private set; }

    public int DropoffCalls { get; private set; }

    public PointOfInterest? Pickup()
    {
        PickupCalls++;
        return PickupDoor;
    }

    public PointOfInterest? Dropoff(TouristenDropoffDefinition definition)
    {
        DropoffCalls++;
        return Doors.TryGetValue(definition.Id, out PointOfInterest? poi) ? poi : null;
    }
}
