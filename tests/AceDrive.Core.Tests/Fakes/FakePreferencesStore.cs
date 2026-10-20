using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>In-memory preferences: the last save is what a restart would load.</summary>
internal sealed class FakePreferencesStore : IPreferencesStore
{
    public FakePreferencesStore(PlayerPreferences? initial = null) =>
        Saved = initial ?? PlayerPreferences.Default;

    public PlayerPreferences Saved { get; private set; }

    public PlayerPreferences Load() => Saved;

    public void Save(PlayerPreferences preferences) => Saved = preferences;
}
