// The player's first run choices, kept apart from onboarding progress and the
// profile: the HUD and every screen read them, and they survive a re-run of the
// introduction. Persisted via IPreferencesStore as preferences.json.
namespace AceDrive.Core;

/// <summary>Distance unit + UI language chosen on the first-run page.</summary>
public sealed record PlayerPreferences(MileageUnit Units, UiLanguage Language)
{
    /// <summary>What a fresh install reads before the page is answered.</summary>
    public static readonly PlayerPreferences Default =
        new(MileageUnit.Kilometres, UiLanguage.English);
}
