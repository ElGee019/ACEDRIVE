// Draws the two addresses of a taxi proposal (PORTPLAN item 11, Unity
// TaxiJobGenerator steps 1-2): one house on the customer's street, one on a
// second street, with the optional radius around the player's home applied
// before any name is resolved. Pure and seeded.
namespace AceDrive.Core;

/// <summary>
/// Unity's address draw: filter the map's houses by the radius, group them by
/// street, require at least two streets, then pick one street and one house
/// for each leg. The two legs always sit on different streets, so the phrase
/// can say "home" at one door and "the {surname} residence" at the other.
/// </summary>
public static class TaxiAddressPlan
{
    /// <summary>
    /// False when the map has fewer than two streets with a usable house
    /// inside the radius; the caller then keeps the page's fallback copy.
    /// Never throws for an invalid map or geometry.
    /// </summary>
    public static bool TryDraw(
        AddressMap map, Random rng, AddressLocation? home, int radiusKm,
        out AddressLocation customerHome, out AddressLocation secondResidence)
    {
        customerHome = default;
        secondResidence = default;

        int radius = TaxiJobRadius.Effective(radiusKm);
        AddressLocation origin = home.GetValueOrDefault();
        bool limited = radius > 0 && home.HasValue;

        var streets = new List<AddressStreet>();
        var houses = new List<List<int>>();
        foreach (AddressStreet street in map.Streets)
        {
            int count = HouseNumberPlan.Count(street, map);
            if (count <= 0)
            {
                continue;
            }

            var valid = new List<int>(count);
            for (int number = 1; number <= count; number++)
            {
                var address = new AddressLocation(
                    street.Name, number, HouseNumberPlan.SideOf(number));
                if (!limited || AddressBook.WithinRadius(map, origin, address, radius))
                {
                    valid.Add(number);
                }
            }

            if (valid.Count > 0)
            {
                streets.Add(street);
                houses.Add(valid);
            }
        }

        if (streets.Count < 2)
        {
            return false;
        }

        int firstStreet = rng.Next(streets.Count);
        int firstNumber = houses[firstStreet][rng.Next(houses[firstStreet].Count)];

        // Pick uniformly among the OTHER streets; the shift avoids a retry
        // loop and keeps the same distribution as Unity's do/while.
        int secondStreet = rng.Next(streets.Count - 1);
        if (secondStreet >= firstStreet)
        {
            secondStreet++;
        }
        int secondNumber = houses[secondStreet][rng.Next(houses[secondStreet].Count)];

        customerHome = new AddressLocation(
            streets[firstStreet].Name, firstNumber, HouseNumberPlan.SideOf(firstNumber));
        secondResidence = new AddressLocation(
            streets[secondStreet].Name, secondNumber, HouseNumberPlan.SideOf(secondNumber));
        return true;
    }
}
