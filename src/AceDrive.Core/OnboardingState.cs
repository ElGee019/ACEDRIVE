// Onboarding data (PORTPLAN Career Start items 2-4): favorite brand ->
// grandma's car -> birthday dialogue. Progress persists via IOnboardingStore.
namespace AceDrive.Core;

/// <summary>Persisted onboarding progress.</summary>
public sealed record OnboardingState(string? FavoriteBrand, string? GrandmaCarId, bool BirthdayDone)
{
    public static readonly OnboardingState Fresh = new(null, null, false);
    public bool IsComplete => BirthdayDone;
}
