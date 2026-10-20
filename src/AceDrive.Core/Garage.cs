// The multi-car garage (PORTPLAN item 30, port of Unity GarageScreen +
// PlayerData.ownedCars/grandmaCar). Owns the owned cars, the current car and the
// catalogue lookups the garage page, the dealer and the odometer feed need. Unity
// parity: no slot cap, no selling, no depreciation — the list only grows, and the
// current car is whatever the player last picked. Mutable state holder like
// DriverWallet; every mutation is a cold path (a purchase, a car switch, a load).
namespace AceDrive.Core;

public sealed class Garage
{
    private readonly IReadOnlyDictionary<string, CarSpec> _catalog;
    private readonly List<OwnedCar> _cars = [];
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
    private readonly FilthRules _filthRules;
    private readonly MaintenanceRules _maintenanceRules;
    private readonly int _seed;

    public Garage(IReadOnlyList<CarSpec> catalog, GarageState? saved = null,
        FilthRules? filthRules = null, MaintenanceRules? maintenanceRules = null, int seed = 0)
    {
        var byKey = new Dictionary<string, CarSpec>(StringComparer.Ordinal);
        foreach (CarSpec car in catalog)
        {
            // A catalogue with a duplicate key is a data bug; first row wins so
            // the lookup can never throw mid-session (JsonCarStore already drops
            // duplicate rows on load).
            byKey.TryAdd(car.Key, car);
        }
        _catalog = byKey;
        // The filth cap/price the fleet's cars were built with (PORTPLAN item
        // 33) and the condition tables behind wear/engine (items 31/32).
        // Defaults keep the test shell and hand-made garages working; the
        // composition root passes the loaded data instances.
        _filthRules = filthRules ?? FilthRules.Default;
        _maintenanceRules = maintenanceRules ?? MaintenanceRules.Default;
        // WHY a seed: each car's engine schedule draws a random minute of day
        // (Unity EngineHealthService). A seeded instance keeps that
        // deterministic in tests and off the shared global (AGENTS.md).
        _seed = seed;
        Resume(saved);
    }

    /// <summary>The filth rules every owned car reads (PORTPLAN item 33):
    /// exposed so the HUD line and the wash page price their copy from the
    /// same data the readings clamp against.</summary>
    public FilthRules FilthRules => _filthRules;

    /// <summary>The condition tables behind wear/engine (PORTPLAN items 31/32):
    /// exposed so the mechanic page prices parts and reads breakdown chance from
    /// the same data the cars were built with.</summary>
    public MaintenanceRules MaintenanceRules => _maintenanceRules;

    /// <summary>Owned cars, ordered by key (Unity sorts the garage the same way).</summary>
    public IReadOnlyList<OwnedCar> Cars => _cars;

    /// <summary>Keys of every owned car: the dealer's exclusion set (item 29).</summary>
    public IReadOnlySet<string> Keys => _keys;

    /// <summary>The car the player drives now, null on an empty garage.</summary>
    public OwnedCar? Current => CurrentKey is { } key ? Find(key) : null;

    public string? CurrentKey { get; private set; }

    public OwnedCar? Find(string key)
    {
        foreach (OwnedCar car in _cars)
        {
            if (string.Equals(car.Key, key, StringComparison.Ordinal))
            {
                return car;
            }
        }
        return null;
    }

    public bool Owns(string key) => _keys.Contains(key);

    /// <summary>The catalogue row behind an owned car, null when the catalogue no
    /// longer lists that key (a retired variant still shows as driven, unnamed).</summary>
    public CarSpec? SpecOf(OwnedCar car) => _catalog.GetValueOrDefault(car.Key);

    /// <summary>
    /// Takes delivery of a car: Unity's OwnedCar defaults — odometer 0, no
    /// insurance. The player's own pick (grandma's car) is current straight away;
    /// a dealer purchase stays a garage entry until the player chooses to drive
    /// it (BoughtFirstCar), so only an empty garage auto-selects.
    /// </summary>
    public OwnedCar Add(CarSpec car, int week) => Add(car.Key, week);

    /// <summary>Starter-car twin: the onboarding pick is a catalogue key too.</summary>
    public OwnedCar Add(string key, int week)
    {
        OwnedCar car = new(key, week, _filthRules, _maintenanceRules, _seed);
        _cars.Add(car);
        _keys.Add(key);
        _cars.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
        CurrentKey ??= key;
        return car;
    }

    /// <summary>
    /// Switches the current car (Unity GarageScreen.OnChangeClicked). Returns
    /// false for a car the garage does not hold: the caller's save stays honest
    /// instead of pointing at a key that would load as no current car.
    /// </summary>
    public bool SetCurrent(string key)
    {
        if (!_keys.Contains(key))
        {
            return false;
        }
        if (string.Equals(CurrentKey, key, StringComparison.Ordinal))
        {
            return false;
        }
        CurrentKey = key;
        return true;
    }

    /// <summary>What the next session reopens (Unity SaveManager.Save of PlayerData).</summary>
    public GarageState Snapshot()
    {
        OwnedCarState[] cars = new OwnedCarState[_cars.Count];
        for (int i = 0; i < cars.Length; i++)
        {
            cars[i] = _cars[i].Snapshot();
        }
        return new GarageState(CurrentKey, cars);
    }

    // Resume: only rows the store could not validate are already gone; here the
    // pure-logic guard runs too (a hand-edited file, a duplicate key, a negative
    // odometer), so a broken save degrades to a smaller garage, never a crash.
    private void Resume(GarageState? saved)
    {
        if (saved?.Cars is not { Count: > 0 } rows)
        {
            return;
        }
        foreach (OwnedCarState row in rows)
        {
            string key = (row.Key ?? "").Trim();
            // A broken row (no key, a duplicate, a negative or non-finite
            // odometer) is dropped rather than clamped: the state that
            // survives must be the one the file actually recorded. The key
            // registers last, so a rejected row cannot claim a good row's key.
            if (key.Length == 0
                || row.Odometer < 0.0 || double.IsNaN(row.Odometer) || double.IsInfinity(row.Odometer)
                || !_keys.Add(key))
            {
                continue;
            }
            _cars.Add(new OwnedCar(key, Math.Max(0, row.PurchaseWeek), _filthRules, _maintenanceRules, _seed, row));
        }
        _cars.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
        if (saved.CurrentKey is { Length: > 0 } current && _keys.Contains(current))
        {
            CurrentKey = current;
        }
        else if (_cars.Count > 0)
        {
            CurrentKey = _cars[0].Key;
        }
    }
}
