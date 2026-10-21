using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The slice of the shell the voice router needs: what is on screen, how to
/// wake the panel, and the content tables the matcher reads. Keeps the router
/// free of the whole MainViewModel surface (interface segregation).
/// </summary>
public interface IVoiceHost
{
    /// <summary>True while the overlay panel is visible.</summary>
    bool IsShown { get; }

    /// <summary>The active screen, or null; only <see cref="IVoiceScreen"/> ones answer to voice.</summary>
    object? CurrentScreen { get; }

    /// <summary>The brand the wake word listens for (the car currently driven).</summary>
    string CarBrand { get; }

    /// <summary>Content tables (aliases + transcript confusions) read on demand.</summary>
    OnboardingContent Content { get; }

    /// <summary>Reveals the panel (the wake-word path).</summary>
    void Show();

    /// <summary>Counts as user presence for the idle auto-hide window.</summary>
    void RecordActivity();
}
