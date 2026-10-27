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
/// destination (TOZONES §4.7).
/// </summary>
public sealed class TaxiJobProposer(OperatingZone zone, ResidentBook residents, int seed)
{
    private readonly Random _rng = new(seed);

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
        ProceduralName customer = (hasAddresses
            ? residents.GetOrCreate(customerHome).Name
            : residents.DrawName()).ForCapacity(passengerCapacity);
        ProceduralName secondResident = hasAddresses
            ? residents.GetOrCreate(secondResidence).Name
            : residents.DrawName();

        return new TaxiProposal(
            customer,
            secondResident,
            _rng.Next(2) == 0,
            hasAddresses ? customerHome : null,
            hasAddresses ? secondResidence : null);
    }
}
