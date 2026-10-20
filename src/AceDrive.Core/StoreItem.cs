// One row of the item database (PORTPLAN item 26, port of Unity's
// StoreItemDef): the shelf price, the trunk volume and the hunger a bite
// removes. Pure data; the store page formats it, the catalog owns it.
namespace AceDrive.Core;

/// <summary>One buyable item: price in CR, volume in litres, hunger reduction.</summary>
public sealed record StoreItem(
    string Name,
    double Price,
    double Volume,
    int Hunger,
    string Category);
