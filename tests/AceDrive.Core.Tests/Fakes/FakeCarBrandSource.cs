using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeCarBrandSource : ICarBrandSource
{
    public event EventHandler<CarBrandChangedEventArgs>? BrandChanged;
    public string? Primed { get; set; }
    public string Prime(string fallback) => Primed ?? fallback;
    public void Fire(string brand) => BrandChanged?.Invoke(this, new CarBrandChangedEventArgs(brand));
    public void StartWatching() { }
    public void StopWatching() { }
    public void Dispose() { }
}
