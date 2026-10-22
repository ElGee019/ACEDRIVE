// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 61 and LATESTDRIVEBS2
// FirestoreService.SanitizeDocId.
namespace AceDrive.Core;

/// <summary>
/// Firestore doc key for one country's aggregate. Lowercase, every
/// non-alphanumeric character becomes a dash, and a name with no
/// alphanumeric at all degrades to "unknown".
/// </summary>
public static class CountryKey
{
    public static string Normalize(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return "unknown";
        }

        string raw = country.Trim().ToLowerInvariant();
        System.Text.StringBuilder sb = new(raw.Length);
        bool hasAlnum = false;
        foreach (char c in raw)
        {
            bool isAlnum = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
            if (isAlnum) hasAlnum = true;
            sb.Append(isAlnum ? c : '-');
        }
        return hasAlnum ? sb.ToString() : "unknown";
    }
}
