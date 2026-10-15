using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Supplies the content bundle (brands, voice aliases, dialogue). Readers
/// may hand out a refreshed snapshot when the Data files change, so
/// re-tuning the wake tables no longer requires an overlay restart.
/// </summary>
public interface IContentSource
{
    OnboardingContent Content { get; }
}
