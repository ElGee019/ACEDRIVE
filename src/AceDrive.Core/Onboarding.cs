// Onboarding data: favorite brand -> grandma's car -> birthday dialogue.
// Pure data records, no behavior. Content loads from Data/*.json at startup
// (Services), progress persists via IOnboardingStore. Spec: PORTPLAN Career
// Start items 2-4; behavior parity with the evo-companion flow.
namespace AceDrive.Core;

/// <summary>One of grandma's starter cars. ImagePath is null when no thumbnail ships.</summary>
public sealed record GrandmaCar(string Id, string Name, int Year, int Price, string? ImagePath);

/// <summary>One birthday dialogue page. Speaker is "GRANDMA", "YOU", or empty.</summary>
public sealed record DialogueLine(string Speaker, string Text);

/// <summary>Persisted onboarding progress.</summary>
public sealed record OnboardingState(string? FavoriteBrand, string? GrandmaCarId, bool BirthdayDone)
{
    public static readonly OnboardingState Fresh = new(null, null, false);
    public bool IsComplete => BirthdayDone;
}

/// <summary>Onboarding content bundle handed to the screen ViewModels.</summary>
public sealed record OnboardingContent(
    IReadOnlyList<string> Brands,
    IReadOnlyDictionary<string, string[]> Aliases,
    IReadOnlyList<GrandmaCar> Cars,
    IReadOnlyList<DialogueLine> Dialogue);
