using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The slice of the session the fleet reads: what EVO reports as driven and
/// whether the introduction is behind us. Keeps the service independent of the
/// whole shell (interface segregation).
/// </summary>
public interface IFleetHost
{
    /// <summary>The catalogue identity behind the driven car, or null while the
    /// log watcher has not announced one.</summary>
    CarIdentity? CarIdentity { get; }

    /// <summary>The brand the watcher published (the wake word's brand).</summary>
    string CarBrand { get; }

    /// <summary>Brand spellings the onboarding content knows, for the receipt's
    /// canonical brand line.</summary>
    IReadOnlyList<string> Brands { get; }

    /// <summary>True once the birthday dialogue is behind us (Unity's
    /// firstSceneComplete gate).</summary>
    bool OnboardingDone { get; }
}
