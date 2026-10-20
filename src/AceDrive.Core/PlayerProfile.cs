// Player identity and home address from Career Start Screen 1. Persisted
// separately from onboarding progress and the driver wallet (profile.json).
namespace AceDrive.Core;

/// <summary>The name and home address confirmed on the opening screen.</summary>
public sealed record PlayerProfile(
    string DisplayName,
    string Country,
    string City,
    string StreetId,
    int HouseNumber)
{
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(DisplayName)
        && !string.IsNullOrWhiteSpace(Country)
        && !string.IsNullOrWhiteSpace(City)
        && !string.IsNullOrWhiteSpace(StreetId)
        && HouseNumber > 0;
}
