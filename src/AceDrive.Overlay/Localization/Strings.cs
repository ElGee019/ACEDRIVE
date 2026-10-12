using System.Globalization;
using System.Resources;

namespace AceDrive.Overlay.Localization;

/// <summary>
/// Code-side access to Resources/Strings.*.resx. XAML uses <c>LocExtension</c>.
/// Falls back to the key itself so a missing translation degrades to a key,
/// never to an exception.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Rm = new(
        "AceDrive.Overlay.Resources.Strings", typeof(Strings).Assembly);

    public static string Get(string key) =>
        Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    // Lookup follows the UI culture; number/date formatting follows the
    // user's formatting culture. The two are intentionally different.
    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
