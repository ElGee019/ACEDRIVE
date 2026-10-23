// Progressive business POIs (PORTPLAN item 24, port of Unity's street/job
// generators). A new game starts with no business doors: the operating zone
// and the settled-job count open them one threshold at a time, always on a
// free house. Pure logic: the host banks the returned doors, never this class.
namespace AceDrive.Core;

/// <summary>
/// Grows the POI directory from the career's progress. Each definition that
/// carries a <see cref="PoiSpawnRule"/> is compared against the saved
/// progress; every missing door is drawn on a random street at a house no
/// known POI uses, with a procedural owner name. Business families gate on
/// streets/jobs, store families on an owned hub category. Randomness is a
/// seeded instance, never the shared global (AGENTS), so tests pin the
/// addresses.
/// </summary>
public sealed class PoiSpawner
{
    private readonly AddressMap _map;
    private readonly IReadOnlyList<PoiDefinition> _definitions;
    private readonly ProceduralNameGenerator _names;
    private readonly Random _rng;

    public PoiSpawner(
        AddressMap map, IReadOnlyList<PoiDefinition> definitions,
        ProceduralNameGenerator names, int seed)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _definitions = definitions ?? [];
        _names = names ?? throw new ArgumentNullException(nameof(names));
        _rng = new Random(seed);
    }

    /// <summary>
    /// The doors the saved progress has unlocked and <paramref name="known"/>
    /// does not have yet, in definition order. The caller adds them to the
    /// directory and banks the file. Empty when no threshold is crossed or the
    /// map has no free house; never throws, never returns a duplicate id or a
    /// used address. <paramref name="ownsCategory"/> gates the store families:
    /// no owned grocery/coffee hub means no store door.
    /// </summary>
    public IReadOnlyList<PointOfInterest> Spawn(
        int discoveredStreets, int settledJobs, PoiDirectory known,
        Predicate<string>? ownsCategory = null)
    {
        var added = new List<PointOfInterest>();
        if (known is null || _map.Streets is not { Count: > 0 })
        {
            return added;
        }

        foreach (PoiDefinition definition in _definitions)
        {
            if (definition.Spawn is not { } rule
                || rule.Pacing == PoiPacing.None
                || string.IsNullOrWhiteSpace(definition.NamePattern))
            {
                continue;
            }

            int existing = CountOfType(known.All, definition.Type)
                + CountOfType(added, definition.Type);
            bool categoryOwned = definition.Category.Length > 0
                && ownsCategory is not null
                && ownsCategory(definition.Category);
            int expected = rule.ExpectedCount(
                discoveredStreets, settledJobs, categoryOwned);
            while (existing < expected)
            {
                PointOfInterest? poi = SpawnOne(definition, existing, known, added);
                if (poi is null)
                {
                    // The map is full: stop this family, leave the rest alone.
                    break;
                }

                added.Add(poi);
                existing++;
            }
        }

        return added;
    }

    private PointOfInterest? SpawnOne(
        PoiDefinition definition, int index,
        PoiDirectory known, List<PointOfInterest> added)
    {
        if (DrawFreeAddress(known, added) is not { } door)
        {
            return null;
        }

        ProceduralName owner = _names.NextPerson();
        string name = DisplayName(definition, owner);

        return new PointOfInterest(
            UniqueId(definition.Type, index, known, added),
            name,
            door,
            definition.Type,
            owner.BaseName,
            definition.Category,
            definition.GuaranteedItems);
    }

    // A translated or hand-edited pattern is content, not code: a missing
    // placeholder or a stray {1} must leave the fixed display name instead of
    // taking the settle down (AGENTS: Try* at every external boundary).
    private static string DisplayName(
        PoiDefinition definition, ProceduralName owner)
    {
        if (string.IsNullOrWhiteSpace(owner.FirstName)
            || string.IsNullOrWhiteSpace(definition.NamePattern))
        {
            return definition.Name;
        }

        try
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                definition.NamePattern, owner.FirstName);
        }
        catch (FormatException)
        {
            return definition.Name;
        }
    }

    // A street is picked at random, then a free number on it: the same even
    // spread the map uses everywhere, with known doors (story and generated)
    // excluded so two businesses never share an address.
    private AddressLocation? DrawFreeAddress(
        PoiDirectory known, List<PointOfInterest> added)
    {
        int streetCount = _map.Streets.Count;
        int start = _rng.Next(streetCount);
        for (int step = 0; step < streetCount; step++)
        {
            AddressStreet street = _map.Streets[(start + step) % streetCount];
            int houses = HouseNumberPlan.Count(street, _map);
            if (houses <= 0)
            {
                continue;
            }

            int[] taken = TakenNumbers(street.Name, houses, known, added);
            int free = houses - taken.Length;
            if (free <= 0)
            {
                continue;
            }

            int number = FreeNumber(_rng.Next(free), houses, taken);
            if (number > 0)
            {
                return new AddressLocation(
                    street.Name, number, HouseNumberPlan.SideOf(number));
            }
        }

        return null;
    }

    private static int[] TakenNumbers(
        string streetName, int houses,
        PoiDirectory known, List<PointOfInterest> added)
    {
        var numbers = new List<int>();
        CollectTaken(streetName, houses, known.All, numbers);
        CollectTaken(streetName, houses, added, numbers);
        return [.. numbers];
    }

    private static void CollectTaken(
        string streetName, int houses,
        IReadOnlyList<PointOfInterest> pois, List<int> numbers)
    {
        foreach (PointOfInterest poi in pois)
        {
            int number = poi.Address.HouseNumber;
            if (string.Equals(
                    poi.Address.StreetName, streetName, StringComparison.OrdinalIgnoreCase)
                && number >= 1 && number <= houses
                && !numbers.Contains(number))
            {
                numbers.Add(number);
            }
        }
    }

    // Walks 1..houses, skipping the taken numbers, and answers the slot-th
    // free one (slots are 0-based and always < the free count).
    private static int FreeNumber(int slot, int houses, int[] taken)
    {
        for (int number = 1; number <= houses; number++)
        {
            if (Array.IndexOf(taken, number) >= 0)
            {
                continue;
            }

            if (slot == 0)
            {
                return number;
            }

            slot--;
        }

        return 0;
    }

    // Unity's "<type>_<index>" convention: the screener routes on the type,
    // while the id stays unique when a save already holds an older door of
    // the same family.
    private static string UniqueId(
        string type, int index, PoiDirectory known, List<PointOfInterest> added)
    {
        int suffix = Math.Max(0, index);
        while (true)
        {
            string id = $"{type}_{suffix}";
            if (!known.Contains(id) && !ContainsId(added, id))
            {
                return id;
            }

            suffix++;
        }
    }

    private static bool ContainsId(List<PointOfInterest> pois, string id)
    {
        foreach (PointOfInterest poi in pois)
        {
            if (string.Equals(poi.Id, id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountOfType(
        IReadOnlyList<PointOfInterest> pois, string type)
    {
        int count = 0;
        foreach (PointOfInterest poi in pois)
        {
            if (string.Equals(poi.Type, type, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }
}
