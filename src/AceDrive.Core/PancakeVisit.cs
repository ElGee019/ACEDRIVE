// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate it.
// Spec source: PORTPLAN items 26/52, port of Unity's Grandma pancake visit
// (ItemDatabase's Grandma's Pancakes, hungerEventTriggered in
// GameClockManager.UpdateHunger).
namespace AceDrive.Core;

/// <summary>
/// Grandma's pancakes: the one food the catalogue hands over instead of selling.
/// The item's identity lives here because three places ask about it — the intro
/// dialogue that packs the first stack, the weekly visit that packs another and
/// the appetite's one-shot event, which fires only while the trunk carries one —
/// and none of them may hardcode the name. The hunger value and the trunk
/// volume stay in the catalogue row, never in this class, so a balance edit
/// re-prices the plate exactly like it re-prices a shop shelf.
/// </summary>
public static class PancakeVisit
{
    /// <summary>The catalogue row the intro and the weekly visit hand over.</summary>
    public const string ItemName = "GRANDMA'S PANCAKES";

    /// <summary>True while the trunk carries the stack the one-shot waits for.</summary>
    public static bool IsCarried(Trunk trunk)
    {
        ArgumentNullException.ThrowIfNull(trunk);
        return trunk.Contains(ItemName);
    }

    /// <summary>
    /// Packs one stack for the road (the intro dialogue's "take these, love" and
    /// every weekly visit). False when the catalogue lost the row, so the caller
    /// can log why the gift never landed; nothing is added in that case.
    /// </summary>
    public static bool Pack(Trunk trunk, StoreCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(trunk);
        ArgumentNullException.ThrowIfNull(catalog);

        if (catalog.Find(ItemName) is not { } pancakes)
        {
            return false;
        }

        trunk.Add(pancakes, 1);
        return true;
    }

    /// <summary>
    /// One meal at grandma's: the plate comes off the meter by the catalogue
    /// row's hunger value and she packs one more stack for the road. Null when
    /// the catalogue no longer defines a food row for it — nothing is eaten and
    /// nothing is packed, so a hand-edited catalogue can never buy an appetite
    /// drop for free.
    /// </summary>
    public static PancakeMeal? Serve(Trunk trunk, Hunger hunger, StoreCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(trunk);
        ArgumentNullException.ThrowIfNull(hunger);
        ArgumentNullException.ThrowIfNull(catalog);

        if (catalog.Find(ItemName) is not { IsFood: true } pancakes)
        {
            return null;
        }

        int before = hunger.Value;
        hunger.Eat(pancakes.Hunger);
        trunk.Add(pancakes, 1);
        return new PancakeMeal(before, hunger.Value);
    }
}
