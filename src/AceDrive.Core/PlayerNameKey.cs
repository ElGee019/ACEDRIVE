// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md Career Start Screen 1 (2-16 characters) and
// LATESTDRIVEBS2 FirestoreService.NormalizeNameKey/ValidateName.
namespace AceDrive.Core;

/// <summary>
/// Display-name validation and the normalized Firestore doc key used by the
/// global name reservation. Names are trimmed, lowercased, and every run of
/// non-alphanumeric characters collapses to one dash, so "Max Müller" and
/// "max  mueller" reserve different keys but punctuation cannot fake a new
/// identity.
/// </summary>
public static class PlayerNameKey
{
    public const int MinLength = 2;
    public const int MaxLength = 16;

    /// <summary>2-16 characters after trimming, with at least one alphanumeric.</summary>
    public static bool IsValid(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }
        string trimmed = name.Trim();
        return trimmed.Length is >= MinLength and <= MaxLength
            && Normalize(trimmed).Length > 0;
    }

    /// <summary>Firestore doc key for the name; empty when it has no alphanumeric.</summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        string raw = name.Trim().ToLowerInvariant();
        System.Text.StringBuilder sb = new(raw.Length);
        bool hasAlnum = false;
        bool lastDash = false;
        foreach (char c in raw)
        {
            bool isAlnum = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
            if (isAlnum)
            {
                hasAlnum = true;
                sb.Append(c);
                lastDash = false;
            }
            else if (!lastDash)
            {
                sb.Append('-');
                lastDash = true;
            }
        }

        return hasAlnum ? sb.ToString().Trim('-') : string.Empty;
    }
}
