// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: Unity CarHaulPlacementService (the customer address is anywhere
// but the home town), reworked for the placeholder address map: one house on a
// discovered street that is neither the dealer's nor the player's home street,
// PORTPLAN item 20.
namespace AceDrive.Core;

/// <summary>
/// Draws the customer's home door for a haul. Unity places the customer in a
/// nearby town other than the player's own, so the draw here never reuses the
/// dealer's street or the player's home street while a third one exists: the
/// customer's new car is driven away from the dealership and is not delivered
/// next door to the player. The exclusions relax in a fixed order on a small
/// map — home street first, then the dealer's street, then (only when the map
/// has nothing else) a second door on the dealer's own street — so a young
/// career can still be offered hauls. The dealer's own door and the player's
/// own door are never used on any pass, and <paramref name="taken"/> keeps the
/// customer out of a house that already has an occupant, so one citizen lives
/// per house.
/// </summary>
public static class CarHaulAddressPlan
{
    /// <summary>
    /// False when the map has no free house at all, or only the dealer's
    /// single-door street. Never throws for invalid map geometry.
    /// </summary>
    /// <param name="taken">Doors that already have a non-resident occupant — a
    /// POI owner. The caller passes <see cref="PoiDirectory.HasDoor"/>, which
    /// is what keeps one citizen per house.</param>
    public static bool TryDraw(
        AddressMap map, Random rng, AddressLocation dealerDoor,
        AddressLocation? homeDoor, out AddressLocation customerAddress,
        Predicate<AddressLocation>? taken = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(rng);
        customerAddress = default;

        // The doors each street can still offer, built once: a street with no
        // free door left cannot host a customer on any pass.
        var streets = new List<AddressStreet>();
        var free = new List<List<int>>();
        foreach (AddressStreet street in map.Streets)
        {
            int count = HouseNumberPlan.Count(street, map);
            if (count <= 0)
            {
                continue;
            }

            bool isDealerStreet = StreetIs(street.Name, dealerDoor.StreetName);
            var numbers = new List<int>(count);
            for (int number = 1; number <= count; number++)
            {
                // Never the door the car starts at, and never the player's own
                // door, whatever pass allows the street.
                if (isDealerStreet && number == dealerDoor.HouseNumber)
                {
                    continue;
                }

                if (homeDoor is { } player
                    && StreetIs(street.Name, player.StreetName)
                    && number == player.HouseNumber)
                {
                    continue;
                }

                var address = new AddressLocation(
                    street.Name, number, HouseNumberPlan.SideOf(number));
                if (taken is not null && taken(address))
                {
                    continue;
                }

                numbers.Add(number);
            }

            if (numbers.Count > 0)
            {
                streets.Add(street);
                free.Add(numbers);
            }
        }

        if (streets.Count == 0)
        {
            return false;
        }

        // The exclusions relax one at a time; the first pass that can supply a
        // door wins. Keeping the home street outranks keeping the dealer's:
        // delivering to another door on the dealer's block still sends the car
        // away, while delivering to the player's own street reads as a bug.
        foreach ((bool keepDealer, bool keepHome) in Exclusions)
        {
            if (TryPick(
                    streets, free, rng, dealerDoor, homeDoor,
                    keepDealer, keepHome, out customerAddress))
            {
                return true;
            }
        }

        return false;
    }

    // keepDealer/keepHome say whether that street is allowed in the pass.
    private static readonly (bool KeepDealer, bool KeepHome)[] Exclusions =
    [
        (false, false),
        (true, false),
        (false, true),
        (true, true),
    ];

    private static bool TryPick(
        List<AddressStreet> streets, List<List<int>> free, Random rng,
        AddressLocation dealerDoor, AddressLocation? homeDoor,
        bool keepDealer, bool keepHome, out AddressLocation address)
    {
        address = default;
        var candidates = new List<int>(streets.Count);
        for (int index = 0; index < streets.Count; index++)
        {
            bool isDealerStreet = StreetIs(streets[index].Name, dealerDoor.StreetName);
            bool isHomeStreet = homeDoor is { } home
                && StreetIs(streets[index].Name, home.StreetName);

            if ((!keepDealer && isDealerStreet) || (!keepHome && isHomeStreet))
            {
                continue;
            }

            candidates.Add(index);
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        int picked = candidates[rng.Next(candidates.Count)];
        List<int> numbers = free[picked];
        int number = numbers[rng.Next(numbers.Count)];
        address = new AddressLocation(
            streets[picked].Name, number, HouseNumberPlan.SideOf(number));
        return true;
    }

    private static bool StreetIs(string left, string right) =>
        string.Equals(
            left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
