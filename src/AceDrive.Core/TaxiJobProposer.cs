// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 11, Unity
// TaxiJobGenerator steps 1-6.
namespace AceDrive.Core;

/// <summary>
/// Draws a whole proposal like Unity's TaxiJobGenerator: two houses on the
/// operating zone's streets (the customer's home and the second residence,
/// filtered by the radius around the player's home), the residents at those
/// doors from the persisted resident book, and the pickup/drop-off coin flip.
/// Seeded like the ledger, so the proposal sequence replays deterministically
/// in tests.
///
/// The draw reads <see cref="OperatingZone.ZoneMap"/>, never the whole map: a
/// fictional street the zone has not discovered yet must never be offered as a
/// destination (TOZONES §4.7). The player's own door is skipped, but a door a
/// POI owns is not: its occupant is the business owner, so the owner can call a
/// taxi and no resident row is created under them (one citizen per house). A
/// house the resident book already knows stays drawable on purpose: it
/// resolves to the same person every time, which is what makes the town's
/// citizens persist.
/// </summary>
public sealed class TaxiJobProposer(
    OperatingZone zone, ResidentBook residents, PoiDirectory pois, int seed)
{
    private readonly Random _rng = new(seed);
    private readonly PoiDirectory _pois = pois;

    public TaxiProposal Next(
        int passengerCapacity, AddressLocation? home = null, int radiusKm = 0)
    {
        bool hasAddresses = TaxiAddressPlan.TryDraw(
            zone.ZoneMap, _rng, home, radiusKm,
            out AddressLocation customerHome, out AddressLocation secondResidence);

        // Unity resolves the residents before the flip, so a fixed seed sees
        // the same address/name/flip order as the source generator. The
        // data-broken fallback still names the passenger, just without doors.
        // The car decides how much of the drawn party fits on this ride; the
        // resident book still remembers the full identity at the address.
        ProceduralName customer = hasAddresses
            ? Occupant(customerHome).ForCapacity(passengerCapacity)
            : residents.DrawName().ForCapacity(passengerCapacity);
        ProceduralName secondResident = hasAddresses
            ? Occupant(secondResidence)
            : residents.DrawName();

        return new TaxiProposal(
            customer,
            secondResident,
            _rng.Next(2) == 0,
            hasAddresses ? customerHome : null,
            hasAddresses ? secondResidence : null);
    }

    // The person at a door: the business owner when a POI holds the house, a
    // banked resident otherwise (a POI whose owner is unknown leaves the house
    // to the resident book). The owner is never also banked as a resident, so
    // the house still has exactly one citizen and the ride grows the same
    // friendship key the shop's counter does.
    private ProceduralName Occupant(AddressLocation address) =>
        _pois.FindDoor(address) is { OwnerName.Length: > 0 } poi
            ? ProceduralName.FromDisplayName(poi.OwnerName)
            : residents.GetOrCreate(address).Name;
}
