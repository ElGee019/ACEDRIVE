using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The haul's pickup dealer in the headless suite: it answers the dealer the
/// directory already holds, or opens a fixed one the way the real collaborator
/// does, and it can refuse (a map with no free house) so the refusal path stays
/// testable without a full map.
/// </summary>
internal sealed class FakeHaulDealerDoor : IHaulDealerDoor
{
    private readonly PoiDirectory _directory;
    private readonly bool _canOpen;

    public FakeHaulDealerDoor(PoiDirectory directory, bool canOpen = true)
    {
        _directory = directory;
        _canOpen = canOpen;
    }

    public int Calls { get; private set; }

    public PointOfInterest? Dealer()
    {
        Calls++;
        if (!_canOpen)
        {
            return null;
        }

        foreach (PointOfInterest poi in _directory.All)
        {
            if (string.Equals(poi.Type, PoiTypes.Dealer, StringComparison.OrdinalIgnoreCase))
            {
                return poi;
            }
        }

        var door = new PointOfInterest(
            "dealer_0", "Bianchi Cars",
            new AddressLocation("Street 1", 4, HouseSide.Left),
            PoiTypes.Dealer, "Giulia Bianchi");
        _directory.Add(door);
        return door;
    }
}
