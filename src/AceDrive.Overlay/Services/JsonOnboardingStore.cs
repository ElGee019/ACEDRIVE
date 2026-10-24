using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>Onboarding progress in %AppData%\AceDrive\onboarding.json. Never throws.</summary>
public sealed class JsonOnboardingStore : IOnboardingStore
{
    private readonly string _file;

    public JsonOnboardingStore()
        : this(DataPaths.UserSave("onboarding.json"))
    { }

    public JsonOnboardingStore(string file) => _file = file;

    public OnboardingState Load()
    {
        // Corrupt, locked or absent progress just means "start onboarding
        // fresh": the first-run flow re-opens instead of blocking startup.
        JsonFile.TryRead(_file, out OnboardingState? state, out _);
        return state ?? OnboardingState.Fresh;
    }

    public void Save(OnboardingState state)
    {
        // never break shutdown for persistence
        JsonFile.TryWrite(_file, state, out _);
    }
}
