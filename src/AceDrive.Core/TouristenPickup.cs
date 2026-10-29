// Pure gameplay data for the Saturday Touristenfahrten delivery run.
namespace AceDrive.Core;

/// <summary>
/// One kind of pickup shop the run can start from: the POI family and shop
/// category it is placed as, the company names a fresh door may draw, and the
/// item ranges its orders are composed from. Coffee shops and pizzerias are the
/// two rows today; a third kind is a new row in the data file.
/// </summary>
/// <param name="Kind">Stable kind key ("coffee", "pizza").</param>
/// <param name="PoiType">POI family the pickup door is placed as.</param>
/// <param name="Category">Shop category the door carries (the delivery board
/// and the shop shelf layouts read it; empty for a non-shop).</param>
/// <param name="Names">Company names a newly placed door may draw.</param>
/// <param name="Items">Item ranges this shop's orders are drawn from.</param>
public sealed record TouristenPickup(
    string Kind,
    string PoiType,
    string Category,
    IReadOnlyList<string> Names,
    IReadOnlyList<TouristenItemRule> Items);
