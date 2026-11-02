// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: Unity CarHaulOfferService.TryBuildVehicleOffer + PickCar (brand
// -> class band -> era car -> random dealer), PORTPLAN item 20. The customer
// address/name replace Unity's Overpass + name-pool pair with the placeholder
// map and the caller's name source.
namespace AceDrive.Core;

/// <summary>
/// Draws one car-haul offer like Unity's offer service: a brand the player is
/// loyal to (the gate is <see cref="CarHaulPolicy"/>'s), a car of that brand
/// inside the loyalty band, a dealer door, and the customer's home door plus
/// name. Seeded through the injected <see cref="Random"/> so a test replays the
/// same offer sequence.
///
/// No year cap: ACEDRIVE has no year system (GARAGETODO decision), so Unity's
/// "era-eligible" filters and its year half of the fallbacks collapse away.
/// A brand with loyalty but no car row in the database simply cannot be drawn;
/// Unity would reach the same "no car" failure through its fallbacks.
/// </summary>
public sealed class CarHaulOfferService
{
    private readonly Random _rng;

    public CarHaulOfferService(Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        _rng = rng;
    }

    /// <summary>
    /// Draws an offer, or null when no gate/pool can supply one: no brand at
    /// or above the policy's floor, no car in the brand's band (and no
    /// fallback), no dealer door with a resolvable address, or no customer
    /// door on the map. The caller flashes the matching hint; the draw never
    /// throws for empty pools. <see cref="CarHaulDraw.BlockedByBrandLoyalty"/>
    /// separates the loyalty floor from the world-data gaps, because that is
    /// the only refusal the player can act on.
    /// </summary>
    /// <param name="homeDoor">The player's home door, kept out of the customer
    /// draw while the map has another street; null before onboarding resolves
    /// it.</param>
    /// <param name="customerAt">Resolves the customer's identity at the drawn
    /// door (Unity's name pool per cultural zone; the port's generator).
    /// Called once, only after the door is drawn.</param>
    public CarHaulDraw TryDraw(
        IReadOnlyList<CarSpec> cars,
        IReadOnlyList<PointOfInterest> dealers,
        AddressMap map,
        AddressLocation? homeDoor,
        Func<string, double> brandLoyaltyPercent,
        Func<AddressLocation, ProceduralName> customerAt,
        CarHaulPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(cars);
        ArgumentNullException.ThrowIfNull(dealers);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(brandLoyaltyPercent);
        ArgumentNullException.ThrowIfNull(customerAt);
        ArgumentNullException.ThrowIfNull(policy);

        List<(string Brand, double Percent)> eligible =
            EligibleBrands(cars, brandLoyaltyPercent, policy);
        if (eligible.Count == 0)
        {
            return new CarHaulDraw(null, BlockedByBrandLoyalty: true);
        }

        // Unity's draw order: brand, then car, then dealer. Keeping it makes a
        // fixed seed reproduce the same offer the source generator would.
        (string brand, double percent) = eligible[_rng.Next(eligible.Count)];
        CarHaulClassBand band = CarHaulClassBand.For(percent);
        CarSpec? car = PickCar(cars, brand, band);
        if (car is null)
        {
            return new CarHaulDraw(null, BlockedByBrandLoyalty: false);
        }

        PointOfInterest? dealer = PickDealer(dealers);
        if (dealer is null
            || !CarHaulAddressPlan.TryDraw(
                map, _rng, dealer.Address, homeDoor, out AddressLocation customerAddress))
        {
            return new CarHaulDraw(null, BlockedByBrandLoyalty: false);
        }

        return new CarHaulDraw(
            new CarHaulOffer(
                car, dealer, customerAddress, customerAt(customerAddress), percent),
            BlockedByBrandLoyalty: false);
    }

    // One entry per distinct car brand (first-seen order, so the pool order is
    // the data file's and a seed is stable), keeping the brands the policy
    // unlocks.
    private static List<(string Brand, double Percent)> EligibleBrands(
        IReadOnlyList<CarSpec> cars, Func<string, double> loyalty, CarHaulPolicy policy)
    {
        var brands = new List<(string Brand, double Percent)>();
        foreach (CarSpec car in cars)
        {
            if (string.IsNullOrEmpty(car.Brand) || ContainsBrand(brands, car.Brand))
            {
                continue;
            }

            double percent = loyalty(car.Brand);
            if (policy.UnlocksOffer(percent))
            {
                brands.Add((car.Brand, percent));
            }
        }

        return brands;
    }

    private static bool ContainsBrand(List<(string Brand, double Percent)> brands, string brand)
    {
        foreach ((string known, _) in brands)
        {
            if (string.Equals(known, brand, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Unity's pick for a drawn brand: in-band first, then the nearest class of
    /// the same brand. Unity's third fallback ("any eligible brand's in-band
    /// car") cannot trigger here because <see cref="EligibleBrands"/> only
    /// offers brands the car pool actually contains, so the same-brand nearest
    /// class always answers.
    /// </summary>
    private CarSpec? PickCar(IReadOnlyList<CarSpec> cars, string brand, CarHaulClassBand band)
    {
        var inBand = new List<CarSpec>();
        var nearest = new List<CarSpec>();
        int nearestDistance = int.MaxValue;

        foreach (CarSpec car in cars)
        {
            if (!string.Equals(car.Brand, brand, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (band.Contains(car.Class))
            {
                inBand.Add(car);
                continue;
            }

            int distance = car.Class < band.MinClass
                ? band.MinClass - car.Class
                : car.Class - band.MaxClass;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest.Clear();
                nearest.Add(car);
            }
            else if (distance == nearestDistance)
            {
                nearest.Add(car);
            }
        }

        if (inBand.Count > 0)
        {
            return inBand[_rng.Next(inBand.Count)];
        }

        return nearest.Count > 0 ? nearest[_rng.Next(nearest.Count)] : null;
    }

    // A dealer without a resolvable street cannot host a pickup: Unity skips
    // those rows before the reservoir sample, so they are filtered up front.
    private PointOfInterest? PickDealer(IReadOnlyList<PointOfInterest> dealers)
    {
        var resolvable = new List<PointOfInterest>(dealers.Count);
        foreach (PointOfInterest dealer in dealers)
        {
            if (dealer.Type == PoiTypes.Dealer
                && !string.IsNullOrEmpty(dealer.Address.StreetName))
            {
                resolvable.Add(dealer);
            }
        }

        return resolvable.Count > 0 ? resolvable[_rng.Next(resolvable.Count)] : null;
    }
}
