using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The class × rarity pair a delivery receipt prices its XP from: an owned car
/// answers its catalogue class and global rarity, an unowned free-roam car
/// answers 0 × 0 (no XP) because it has no place in the career ledger, and a
/// car the fleet cannot resolve keeps the fallback pair. The career delivery
/// run and the Touristenfahrten run read the same helper, so the two receipts
/// can never price the same car differently.
/// </summary>
internal static class DeliveryCarFacts
{
    internal static (int CarClass, int CarRarity) Of(
        FleetService fleet, ICarRaritySource rarity)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(rarity);

        if (fleet.IsDrivingUnownedCar())
        {
            return (0, 0);
        }

        CarSpec? car = fleet.XpCar();
        int carClass = car is { Class: > 0 } known
            ? known.Class
            : JobSettlement.FallbackCarClass;
        int carRarity = car is not null
            ? rarity.GetRarity(car.Key)
            : JobSettlement.FallbackCarRarity;
        return (carClass, carRarity);
    }
}
