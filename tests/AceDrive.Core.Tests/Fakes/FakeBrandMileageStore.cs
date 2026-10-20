using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeBrandMileageStore : IBrandMileageStore
{
    private readonly Dictionary<string, double> _km = new(StringComparer.OrdinalIgnoreCase);

    public FakeBrandMileageStore(IReadOnlyDictionary<string, double>? seed = null)
    {
        if (seed is not null)
        {
            foreach (var (brand, km) in seed)
            {
                _km[brand] = km;
            }
        }
    }

    public double GetKm(string brand)
    {
        string key = (brand ?? "").Trim();
        return key.Length == 0 ? 0.0 : Math.Max(0.0, _km.GetValueOrDefault(key));
    }

    public double RegisterRide(string brand, double distanceKm)
    {
        string key = (brand ?? "").Trim();
        if (key.Length == 0)
        {
            return 0.0;
        }
        double total = Math.Max(0.0, _km.GetValueOrDefault(key)) + Math.Max(0.0, distanceKm);
        _km[key] = total;
        return total;
    }
}
