using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeCarBrandSource : ICarBrandSource
{
    public event EventHandler<CarBrandChangedEventArgs>? BrandChanged;
    public string? Primed { get; set; }
    public string? CurrentKsId { get; set; }
    public string Prime(string fallback) => Primed ?? fallback;
    public void Fire(string brand, string? ksId = null) =>
        BrandChanged?.Invoke(this, new CarBrandChangedEventArgs(brand, ksId));
    public void StartWatching() { }
    public void StopWatching() { }
    public void Dispose() { }
}
