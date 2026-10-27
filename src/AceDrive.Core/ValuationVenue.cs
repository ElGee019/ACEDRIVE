// Pure gameplay data. The venue is the address the weekly market valuation
// is held at; the HUD pins it and the flow resolves it on arrival.
namespace AceDrive.Core;

/// <summary>
/// One week's valuation venue on the placeholder map: the street it sits on
/// and the house number. A real venue selection (Unity's Overpass town/street
/// discovery) lands with the Eifel map, so the placeholder draw is the only
/// producer today.
/// </summary>
public sealed record ValuationVenue(string StreetId, string StreetName, int HouseNumber)
{
    /// <summary>The house as the HUD and the address formatter read it.</summary>
    public AddressLocation Location =>
        new(StreetName, HouseNumber, HouseNumberPlan.SideOf(HouseNumber));
}
