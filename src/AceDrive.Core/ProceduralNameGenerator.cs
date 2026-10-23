// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11/13/61,
// port of Unity NamePoolManager (Germany zone only).
namespace AceDrive.Core;

/// <summary>
/// Draws passenger identities the way Unity's NamePoolManager does for the
/// Germany cultural zone: weighted roulette over the zone's pools (Unity
/// SelectNamePool), then first name, surname and companion suffix from the
/// picked pool's lists and the shared suffix table. The RNG is a seeded
/// instance, so a given (table, seed) always replays the same sequence —
/// the determinism tests rely on, like RoadTestLedger's own draw.
/// </summary>
public sealed class ProceduralNameGenerator(NamePoolTable table, int seed)
{
    private readonly Random _rng = new(seed);

    public ProceduralName Next()
    {
        ProceduralName person = NextPerson();
        return person with { Suffix = table.Suffixes[_rng.Next(table.Suffixes.Count)] };
    }

    /// <summary>
    /// A bare resident identity (first name + surname) for content that is
    /// not a ride: the business owners behind generated POIs have no companion
    /// suffix. Draws from the same seeded stream, in the same order as the
    /// head of <see cref="Next"/>, so passenger sequences stay unchanged.
    /// </summary>
    public ProceduralName NextPerson()
    {
        NamePool pool = PickPool();
        return new ProceduralName(
            pool.FirstNames[_rng.Next(pool.FirstNames.Count)],
            pool.Surnames[_rng.Next(pool.Surnames.Count)]);
    }

    // Unity's loop kept shape-for-shape: roll inside the total weight,
    // subtract pool by pool, the pool that takes the roll below zero wins;
    // the first pool answers any floating-point drift that never reaches zero.
    private NamePool PickPool()
    {
        double total = 0;
        foreach (NamePool pool in table.Pools)
        {
            total += pool.Weight;
        }

        double roll = _rng.NextDouble() * total;
        foreach (NamePool pool in table.Pools)
        {
            roll -= pool.Weight;
            if (roll <= 0)
            {
                return pool;
            }
        }

        return table.Pools[0];
    }
}
