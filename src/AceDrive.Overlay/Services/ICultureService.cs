using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Applies the player's language to the resource lookup. Every screen reads
/// its copy through Strings, so pointing the UI culture at the chosen
/// satellite is the single switch that turns the overlay Dutch or English.
/// </summary>
public interface ICultureService
{
    void Apply(UiLanguage language);
}
