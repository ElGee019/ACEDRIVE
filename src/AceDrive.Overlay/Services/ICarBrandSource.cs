namespace AceDrive.Overlay.Services;

/// <summary>The player switched to a different car/brand.</summary>
public sealed class CarBrandChangedEventArgs(string brand, string? ksId = null) : EventArgs
{
    public string Brand { get; } = brand;

    /// <summary>ks id of the new car, when the switch announced one.</summary>
    public string? KsId { get; } = ksId;
}

/// <summary>
/// Source of the currently driven car's brand (drives the wake word).
/// Implementations raise BrandChanged from a background thread; consumers
/// marshal to the UI thread themselves.
/// </summary>
public interface ICarBrandSource : IDisposable
{
    event EventHandler<CarBrandChangedEventArgs>? BrandChanged;

    /// <summary>Initial brand from recent history, or the given fallback.</summary>
    string Prime(string fallback);

    /// <summary>ks id behind the current brand, when one was announced.</summary>
    string? CurrentKsId { get; }

    void StartWatching();
    void StopWatching();
}
