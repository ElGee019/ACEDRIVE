// Pure gameplay state for the Saturday Touristenfahrten delivery run.
namespace AceDrive.Core;

/// <summary>
/// The live run: the pickup door, the 1-3 dropoffs in drive order and the
/// index of the leg being driven. The flow owns when the pickup is loaded and
/// when a delivered dropoff hands over to the next; this class only moves the
/// index, so a future Lua port transliterates it.
/// </summary>
public sealed class TouristenRun
{
    private readonly IReadOnlyList<TouristenDropoff> _dropoffs;
    private int _index;

    public TouristenRun(
        PointOfInterest pickup, TouristenPickup pickupKind,
        IReadOnlyList<TouristenDropoff> dropoffs)
    {
        Pickup = pickup ?? throw new ArgumentNullException(nameof(pickup));
        PickupKind = pickupKind ?? throw new ArgumentNullException(nameof(pickupKind));
        _dropoffs = dropoffs ?? throw new ArgumentNullException(nameof(dropoffs));
    }

    /// <summary>The shop the goods are loaded from.</summary>
    public PointOfInterest Pickup { get; }

    /// <summary>The pickup's item table, kept for the run's own logging.</summary>
    public TouristenPickup PickupKind { get; }

    /// <summary>Every dropoff the run plans to make, in drive order.</summary>
    public IReadOnlyList<TouristenDropoff> Dropoffs => _dropoffs;

    /// <summary>True once the goods are on board (the first ENTER).</summary>
    public bool Loaded { get; private set; }

    /// <summary>Index of the dropoff being driven to.</summary>
    public int Index => _index;

    /// <summary>The dropoff being driven to, or null once every one is delivered.</summary>
    public TouristenDropoff? Current =>
        _index < _dropoffs.Count ? _dropoffs[_index] : null;

    /// <summary>True once the load is on board and every dropoff is delivered.</summary>
    public bool IsComplete => Loaded && Current is null;

    /// <summary>Marks the pickup done; the first dropoff becomes current.</summary>
    public void Load() => Loaded = true;

    /// <summary>Moves past the current dropoff and returns the next one, or null.</summary>
    public TouristenDropoff? Advance()
    {
        // Before the load the run is still at the shop: no stop is delivered,
        // so nothing may move on.
        if (Loaded && Current is not null)
        {
            _index++;
        }

        return Current;
    }
}
