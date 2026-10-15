// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 13 (auto taxi
// jobs + radius), port of Unity TaxiJobGenerator's radius rules.
using System.Globalization;

namespace AceDrive.Core;

/// <summary>
/// The optional "how far from home" limit typed on the proposal page
/// (Unity OnRadiusEndEdit): 0 = unlimited, anything below the minimum snaps
/// to it, fractions round to the nearest whole kilometre, and a hearing the
/// mic mangled (or a negative) keeps the previously committed value.
/// </summary>
public static class TaxiJobRadius
{
    /// <summary>Unity TaxiJobGenerator.MinTaxiJobRadiusKm.</summary>
    public const int MinimumKm = 5;

    /// <summary>Commits typed field text to the radius in effect, applying
    /// Unity's end-edit rules. Empty field = 0 (no limit).</summary>
    public static int Normalize(string? raw, int current)
    {
        string trimmed = (raw ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return 0;
        }

        // Both decimal marks answer: the field is typed, not formatted, and
        // the en model transcripts mix "." and "," (Unity's Replace parity).
        if (!double.TryParse(trimmed.Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double parsed) || parsed < 0)
        {
            return current;
        }

        if (parsed == 0)
        {
            return 0;
        }

        if (parsed < MinimumKm)
        {
            return MinimumKm;
        }

        return (int)Math.Round(parsed, MidpointRounding.AwayFromZero);
    }
}
