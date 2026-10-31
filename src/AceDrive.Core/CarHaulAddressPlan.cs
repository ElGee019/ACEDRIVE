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
/// career can still be offered hauls. A street with a single door cannot
/// supply a second address and fails instead of sending the car to the door it
/// starts at.
/// </summary>
public static class CarHaulAddressPlan
{
    /// <summary>
    /// False when the map has no usable house at all, or only the dealer's
    /// single-door street. Never throws for invalid map geometry.
    /// </summary>
    public static bool TryDraw(
        AddressMap map, Random rng, AddressLocation dealerDoor,
        AddressLocation? homeDoor, out AddressLocation customerAddress)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(rng);
        customerAddress = default;

        var streets = new List<AddressStreet>();
        var counts = new List<int>();
        foreach (AddressStreet street in map.Streets)
        {
            int count = HouseNumberPlan.Count(street, map);
            if (count > 0)
            {
                streets.Add(street);
                counts.Add(count);
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
                    streets, counts, rng, dealerDoor, homeDoor,
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
        List<AddressStreet> streets, List<int> counts, Random rng,
        AddressLocation dealerDoor, AddressLocation? homeDoor,
        bool keepDealer, bool keepHome, out AddressLocation address)
    {
        address = default;
        var candidates = new List<int>(streets.Count);
        for (int index = 0; index < streets.Count; index++)
        {
            bool isDealerStreet = string.Equals(
                streets[index].Name, dealerDoor.StreetName, StringComparison.Ordinal);
            bool isHomeStreet = homeDoor is { } home
                && string.Equals(streets[index].Name, home.StreetName, StringComparison.Ordinal);

            if ((!keepDealer && isDealerStreet) || (!keepHome && isHomeStreet))
            {
                continue;
            }

            // A pass that keeps the dealer's street may only use it when it can
            // offer a second door.
            if (isDealerStreet && counts[index] < 2)
            {
                continue;
            }

            candidates.Add(index);
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        int streetIndex = candidates[rng.Next(candidates.Count)];
        AddressStreet drawn = streets[streetIndex];
        int houseCount = counts[streetIndex];
        bool drawnIsDealer = string.Equals(
            drawn.Name, dealerDoor.StreetName, StringComparison.Ordinal);

        int number = drawnIsDealer
            ? AnotherNumberOn(houseCount, dealerDoor.HouseNumber, rng)
            : rng.Next(houseCount) + 1;
        if (number == 0)
        {
            return false;
        }

        address = new AddressLocation(
            drawn.Name, number, HouseNumberPlan.SideOf(number));
        return true;
    }

    // 0 means "no other door on this street": the caller then moves on to the
    // next exclusion pass.
    private static int AnotherNumberOn(int houseCount, int takenNumber, Random rng)
    {
        if (houseCount < 2)
        {
            return 0;
        }

        var numbers = new List<int>(houseCount - 1);
        for (int number = 1; number <= houseCount; number++)
        {
            if (number != takenNumber)
            {
                numbers.Add(number);
            }
        }

        return numbers[rng.Next(numbers.Count)];
    }
}
