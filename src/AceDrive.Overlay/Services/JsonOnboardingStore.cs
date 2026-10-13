using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>Onboarding progress in %AppData%\AceDrive\onboarding.json. Never throws.</summary>
public sealed class JsonOnboardingStore : IOnboardingStore
{
    private readonly string _file;

    public JsonOnboardingStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "onboarding.json"))
    { }

    public JsonOnboardingStore(string file) => _file = file;

    public OnboardingState Load()
    {
        try
        {
            if (!File.Exists(_file)) return OnboardingState.Fresh;
            var state = JsonSerializer.Deserialize<OnboardingState>(File.ReadAllText(_file));
            return state ?? OnboardingState.Fresh;
        }
        catch { return OnboardingState.Fresh; }
    }

    public void Save(OnboardingState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(state));
        }
        catch { /* never break shutdown for persistence */ }
    }
}
