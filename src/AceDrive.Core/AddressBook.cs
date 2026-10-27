// Lookups and closed-lap geometry over the placeholder free-roam map
// (PORTPLAN Step 1 + items 11/13). Pure data: the taxi generator, the
// proposal page and the onboarding home all read the same map, and a future
// Lua port transliterates this file instead of the UI.
namespace AceDrive.Core;

/// <summary>
/// The address map's street/home lookups plus the lap arithmetic the taxi
/// radius needs. House numbers sit at a fixed spacing along their street, so
/// two addresses are as far apart as the shorter of the two ways around the
/// closed lap.
/// </summary>
public static class AddressBook
{
    /// <summary>The street whose id matches, ignoring case.</summary>
    public static AddressStreet? Street(AddressMap map, string? id)
    {
        if (map.Streets is not { Count: > 0 } || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (AddressStreet street in map.Streets)
        {
            if (string.Equals(street.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return street;
            }
        }

        return null;
    }

    /// <summary>
    /// Total drivable length of the placeholder map's base (non-fictional)
    /// streets in km. The interim operating-zone seed until real Eifel street
    /// lengths arrive (PORTPLAN 58): a 0 km zone would pin the starter bonus at
    /// 100%. Fictional rows are the zone's extra labels onto the same lap, so
    /// counting them would inflate the base area.
    /// </summary>
    public static double MapLengthKm(AddressMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        double total = 0.0;
        foreach (AddressStreet street in map.Streets)
        {
            if (!street.Fictional)
            {
                total += StreetLengthKm(map, street);
            }
        }
        return total;
    }

    /// <summary>
    /// One street's own drivable length in km: the span it covers of the lap.
    /// A zone expansion adds exactly this to the zone's area, so a street that
    /// covers less of the lap grows the zone less.
    /// </summary>
    public static double StreetLengthKm(AddressMap map, AddressStreet street)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(street);
        double trackKm = double.IsFinite(map.TrackLengthMeters)
            ? Math.Max(0.0, map.TrackLengthMeters) / 1000.0
            : 0.0;
        double span = street.ToFraction - street.FromFraction;
        return double.IsFinite(span) && span > 0.0
            ? Math.Min(span, 1.0) * trackKm
            : 0.0;
    }

    /// <summary>
    /// Mean base-street length in km: the interim length one zone expansion
    /// adds while street metadata is pending, so the zone (and the starter
    /// bonus) still decays with every expansion. Zero for an empty map.
    /// </summary>
    public static double AverageStreetLengthKm(AddressMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        int baseStreets = 0;
        foreach (AddressStreet street in map.Streets)
        {
            if (!street.Fictional)
            {
                baseStreets++;
            }
        }
        return baseStreets > 0 ? MapLengthKm(map) / baseStreets : 0.0;
    }

    /// <summary>
    /// The streets the operating zone covers, home street first. Interim
    /// mapping: the zone knows a street COUNT (Unity's discoveredStreets) while
    /// the placeholder map names its own two streets, so the profile's home
    /// street leads and the map order fills the rest, capped by the count.
    /// Once the freeroam map gives every zone street its own identity, this
    /// lookup reads the map instead and the signature disappears.
    /// </summary>
    public static IReadOnlyList<AddressStreet> ZoneStreets(
        AddressMap map, int discoveredStreets, string? homeStreetId)
    {
        ArgumentNullException.ThrowIfNull(map);
        int limit = Math.Min(Math.Max(0, discoveredStreets), map.Streets.Count);
        if (limit == 0)
        {
            return [];
        }

        var ordered = new List<AddressStreet>(limit);
        if (!string.IsNullOrWhiteSpace(homeStreetId)
            && Street(map, homeStreetId) is { } home)
        {
            ordered.Add(home);
        }

        foreach (AddressStreet street in map.Streets)
        {
            if (ordered.Count >= limit)
            {
                break;
            }

            if (!ordered.Contains(street))
            {
                ordered.Add(street);
            }
        }

        return ordered;
    }

    /// <summary>The street whose display name matches, ignoring case.</summary>
    public static AddressStreet? StreetByName(AddressMap map, string? name)
    {
        if (map.Streets is not { Count: > 0 } || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (AddressStreet street in map.Streets)
        {
            if (string.Equals(street.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return street;
            }
        }

        return null;
    }

    /// <summary>
    /// The player's home door from the saved profile. Null when the profile
    /// is missing, the street id is unknown, or the saved number no longer
    /// exists on that street (a hand-edited map or an old save); the radius
    /// then behaves like Unity's unset home instead of throwing.
    /// </summary>
    public static AddressLocation? Home(AddressMap map, PlayerProfile? profile)
    {
        if (profile is null)
        {
            return null;
        }

        AddressStreet? street = Street(map, profile.StreetId);
        if (street is null
            || profile.HouseNumber < 1
            || profile.HouseNumber > HouseNumberPlan.Count(street, map))
        {
            return null;
        }

        return new AddressLocation(
            street.Name, profile.HouseNumber, HouseNumberPlan.SideOf(profile.HouseNumber));
    }

    /// <summary>
    /// Distance in metres from the start/finish line to a house, or NaN when
    /// the address does not resolve on the map. Uses the map's stored track
    /// length; the job guidance passes EVO's runtime length instead.
    /// </summary>
    public static double LapOffsetMeters(AddressMap map, AddressLocation address) =>
        LapOffsetMeters(map, address, map.TrackLengthMeters);

    /// <summary>
    /// <see cref="LapOffsetMeters(AddressMap, AddressLocation)"/> at a runtime
    /// track length (EVO's static block), so a track whose length differs from
    /// the map's placeholder still lines up with the HUD's address resolver.
    /// </summary>
    public static double LapOffsetMeters(
        AddressMap map, AddressLocation address, double trackLengthMeters)
    {
        AddressStreet? street = StreetByName(map, address.StreetName);
        if (street is null
            || address.HouseNumber < 1
            || !double.IsFinite(trackLengthMeters)
            || trackLengthMeters <= 0.0
            || !double.IsFinite(map.HouseSpacingMeters)
            || map.HouseSpacingMeters <= 0.0
            || !double.IsFinite(street.FromFraction))
        {
            return double.NaN;
        }

        double offset = street.FromFraction * trackLengthMeters
            + (address.HouseNumber - 1) * map.HouseSpacingMeters;
        return double.IsFinite(offset) ? offset : double.NaN;
    }

    /// <summary>
    /// Closed-lap distance between two houses: the shorter way around the
    /// circuit, so Street 2 No. 33 is next door to Street 1 No. 1. Infinity
    /// when either address does not resolve, which keeps it out of a radius.
    /// </summary>
    public static double DistanceMeters(AddressMap map, AddressLocation a, AddressLocation b) =>
        DistanceMeters(map, a, b, map.TrackLengthMeters);

    /// <summary>
    /// <see cref="DistanceMeters(AddressMap, AddressLocation, AddressLocation)"/>
    /// at a runtime track length.
    /// </summary>
    public static double DistanceMeters(
        AddressMap map, AddressLocation a, AddressLocation b, double trackLengthMeters)
    {
        double offsetA = LapOffsetMeters(map, a, trackLengthMeters);
        double offsetB = LapOffsetMeters(map, b, trackLengthMeters);
        return LapGeometry.DistanceMeters(offsetA, offsetB, trackLengthMeters);
    }

    /// <summary>True when the address lies inside the radius (0 = no limit).</summary>
    public static bool WithinRadius(
        AddressMap map, AddressLocation home, AddressLocation address, int radiusKm)
    {
        int radius = TaxiJobRadius.Effective(radiusKm);
        return radius <= 0 || DistanceMeters(map, home, address) <= radius * 1000.0;
    }

    /// <summary>
    /// Unity's JobFitsRadiusKm: always true when the limit is off, the home
    /// is unset, or the proposal has no addresses (the data-broken fallback).
    /// </summary>
    public static bool JobFitsRadius(
        AddressMap map, AddressLocation? home, AddressLocation? pickup,
        AddressLocation? dropoff, int radiusKm)
    {
        int radius = TaxiJobRadius.Effective(radiusKm);
        if (radius <= 0 || home is not { } origin || pickup is not { } from
            || dropoff is not { } to)
        {
            return true;
        }

        return WithinRadius(map, origin, from, radius)
            && WithinRadius(map, origin, to, radius);
    }
}
