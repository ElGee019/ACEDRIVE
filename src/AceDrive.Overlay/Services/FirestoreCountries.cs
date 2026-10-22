using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>Per-country all-players km aggregate, one write per completed trip.</summary>
public sealed class FirestoreCountries
{
    private readonly IFirestoreDatabase _db;
    private readonly IClock _clock;

    public FirestoreCountries(IFirestoreDatabase db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task<bool> IncrementKmAsync(
        string country, int km, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(country) || km <= 0)
        {
            return Task.FromResult(true);
        }

        Dictionary<string, object?> fields = new(StringComparer.Ordinal)
        {
            ["displayName"] = country.Trim(),
            ["lastUpdatedAt"] = FirebaseOptions.Timestamp(_clock.UtcNow),
        };
        Dictionary<string, long> increments = new(StringComparer.Ordinal)
        {
            ["totalKmDriven"] = km,
        };
        return _db.IncrementAsync(
            FirestoreCollections.Countries + "/" + CountryKey.Normalize(country),
            fields, increments, cancellationToken);
    }
}
