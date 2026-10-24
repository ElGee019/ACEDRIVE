using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>The player switched to a different car/brand.</summary>
public sealed class CarBrandChangedEventArgs(string brand, CarIdentity? identity = null) : EventArgs
{
    public string Brand { get; } = brand;

    /// <summary>Announced car (folder id + mechanical preset when the spawn
    /// line carried one), null when the switch announced no known car.</summary>
    public CarIdentity? Identity { get; } = identity;
}

/// <summary>
/// Source of the currently driven car's brand (drives the wake word) and
/// full identity (drives variant-aware lookups like passenger capacity).
/// Implementations raise BrandChanged from a background thread; consumers
/// marshal to the UI thread themselves.
/// </summary>
public interface ICarBrandSource : IDisposable
{
    event EventHandler<CarBrandChangedEventArgs>? BrandChanged;

    /// <summary>Initial brand from recent history, or the given fallback.</summary>
    string Prime(string fallback);

    /// <summary>
    /// The brand the wake word listens for: the one <see cref="Prime"/> found
    /// or the last switch announced. Empty before the first prime.
    /// </summary>
    string Brand { get; }

    /// <summary>Identity behind the current brand, when one was announced.</summary>
    CarIdentity? CurrentIdentity { get; }

    void StartWatching();
    void StopWatching();
}
