// One store's slice of the town (PORTPLAN item 25, Unity StoreState): the
// citizens it feeds, the units left on its shelves and the fractional growth
// banked but not yet paid out. The same shape is what the city file persists,
// so the saved row and the live cell never drift apart.
namespace AceDrive.Core;

/// <summary>Live economy of one store.</summary>
/// <param name="Key">The <see cref="DeliveryStore"/> this cell belongs to.</param>
/// <param name="Citizens">People the store currently serves.</param>
/// <param name="StockedUnits">Units left on the shelves, the input to the
/// growth band.</param>
/// <param name="GrowthAccumulator">Citizens owed but not yet whole (Unity's
/// growthAccumulator): half a citizen is banked, never shown.</param>
public sealed record StoreState(
    string Key,
    int Citizens,
    int StockedUnits,
    double GrowthAccumulator);
