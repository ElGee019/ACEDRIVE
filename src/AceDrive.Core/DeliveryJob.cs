// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobHandler).
namespace AceDrive.Core;

/// <summary>
/// The live delivery run: the ordered hub pickups and store drop-offs plus
/// the staged money the run settles with. The host drives the HUD from
/// <see cref="Current"/> and calls <see cref="Advance"/> on each confirmed
/// arrival, exactly like Unity's pickup/delivery route indexes.
/// </summary>
public sealed class DeliveryJob
{
    private readonly IReadOnlyList<DeliveryStop> _stops;
    private int _index;

    public DeliveryJob(IReadOnlyList<DeliveryStop> stops, DeliveryRun run)
    {
        _stops = stops ?? throw new ArgumentNullException(nameof(stops));
        Run = run ?? throw new ArgumentNullException(nameof(run));
    }

    /// <summary>The cart's staged money, computed before any shelf moved.</summary>
    public DeliveryRun Run { get; }

    /// <summary>The leg being driven now, or null once every stop is done.</summary>
    public DeliveryStop? Current => _index < _stops.Count ? _stops[_index] : null;

    /// <summary>True once every stop has been confirmed.</summary>
    public bool IsComplete => Current is null;

    /// <summary>Stops confirmed so far (hub pickups and store drop-offs).</summary>
    public int CompletedStops => _index;

    /// <summary>The whole planned route's length, hubs and stores together.</summary>
    public int StopCount => _stops.Count;

    /// <summary>Moves to the next stop and returns it, or null when complete.</summary>
    public DeliveryStop? Advance()
    {
        if (!IsComplete)
        {
            _index++;
        }
        return Current;
    }
}
