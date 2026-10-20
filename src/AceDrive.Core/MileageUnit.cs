// The unit the player reads distances in. AC EVO keeps the same choice in its
// own settings; the overlay stores its own copy because EVO does not publish
// the choice on shared memory (the HUD would otherwise always answer km).
namespace AceDrive.Core;

/// <summary>Unit the odometer and the miles/kilometres readouts use.</summary>
public enum MileageUnit
{
    Kilometres,
    Miles,
}
