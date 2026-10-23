// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 26, port of
// Unity YouScreen.OnTrunkItemClicked: one tap on a carried item consumes one
// unit and lowers the appetite by the catalogue's hunger value.
namespace AceDrive.Core;

/// <summary>
/// One bite from the trunk. The trunk page's EAT tap lands here: the item's
/// unit is removed first, so a stack the player does not hold can never feed
/// them, and only then does the appetite drop. The hunger value comes from the
/// catalogue row the name resolves, never from a copy taken at purchase time —
/// a balance edit re-prices carried goods exactly like it re-prices shelves.
/// </summary>
public static class TrunkEat
{
    /// <summary>
    /// Eats one unit of <paramref name="name"/>. False when the trunk does not
    /// carry it or the catalogue no longer defines it (Unity logs and cannot
    /// consume in that case); nothing changes either way.
    /// </summary>
    public static bool Eat(Trunk trunk, Hunger hunger, StoreCatalog catalog, string name)
    {
        ArgumentNullException.ThrowIfNull(trunk);
        ArgumentNullException.ThrowIfNull(hunger);
        ArgumentNullException.ThrowIfNull(catalog);

        if (catalog.Find(name) is not { } item)
        {
            return false;
        }
        if (!trunk.RemoveOne(name))
        {
            return false;
        }

        hunger.Eat(item.Hunger);
        return true;
    }
}
