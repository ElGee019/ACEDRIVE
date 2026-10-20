using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// Records the language the ViewModel pushed. The real culture switch is
/// <see cref="CultureService"/>'s job and would leak process-wide state into
/// the other headless tests.
/// </summary>
internal sealed class FakeCultureService : ICultureService
{
    public UiLanguage? Applied { get; private set; }

    public void Apply(UiLanguage language) => Applied = language;
}
