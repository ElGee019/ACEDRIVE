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
    string Category)
{
    /// <summary>
    /// True for food: the trunk page files these under the trunk and the rest
    /// — awards, certificates, anything the game hands over — under the glove
    /// compartment. The catalogue's hunger value is the only food signal the
    /// data carries, and it is the same value a bite feeds, so a row that would
    /// feed nothing is not food.
    /// </summary>
    public bool IsFood => Hunger > 0;
}
