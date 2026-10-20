using System.Globalization;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Sets the UI culture only. Number and date formatting deliberately keep
/// following the user's Windows locale (Strings.Format reads CurrentCulture),
/// exactly as before the language switch — picking English does not turn a
/// Dutch machine's decimals into dots.
/// </summary>
public sealed class CultureService : ICultureService
{
    // WHY en-US / nl-NL rather than the neutral codes: the satellites are
    // neutral (Strings.nl.resx), but en-US is the culture the overlay booted
    // on before the first-run page existed, and nl-NL reaches the same nl
    // satellite as the neutral code.
    private static readonly Dictionary<UiLanguage, CultureInfo> CultureByLanguage = new()
    {
        [UiLanguage.English] = CultureInfo.GetCultureInfo("en-US"),
        [UiLanguage.Dutch] = CultureInfo.GetCultureInfo("nl-NL"),
    };

    public void Apply(UiLanguage language)
    {
        CultureInfo culture = CultureByLanguage[language];
        // WHY both: DefaultThreadCurrentUICulture covers threads created from
        // here on; the WPF UI thread already exists, so Strings.Get on it
        // would keep reading the old culture without the second assignment.
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
