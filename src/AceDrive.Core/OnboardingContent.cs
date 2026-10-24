// Onboarding data (PORTPLAN Career Start items 2-4). Content loads from
// Data/*.json at startup (Services); behavior parity with the evo-companion
// flow.
namespace AceDrive.Core;

/// <summary>
/// Onboarding content bundle handed to the screen ViewModels. Aliases and
/// Confusions serve voice control beyond onboarding (wake words app-wide,
/// button-label mishearings app-wide), loaded from Data/*.json.
/// </summary>
public sealed record OnboardingContent(
    IReadOnlyList<string> Brands,
    IReadOnlyDictionary<string, string[]> Aliases,
    IReadOnlyList<GrandmaCar> Cars,
    IReadOnlyList<DialogueLine> Dialogue,
    IReadOnlyList<DialogueLine> PancakeDialogue,
    IReadOnlyList<DialogueLine> CommunityCentreDialogue,
    IReadOnlyList<DialogueLine> MaxDialogue,
    IReadOnlyDictionary<string, string>? Confusions = null);
