using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The shared policy for shipped balance/content tables: a missing or unusable
/// file warns and lets the caller fall back to its built-in defaults. Every
/// table store used to spell this rule out itself (missing check, schema check,
/// two warnings), which is how one of them could quietly drift from the rest.
/// </summary>
internal static class JsonTable
{
    /// <summary>
    /// Reads a shipped table. Returns true only when the file loaded and passed
    /// <paramref name="isValid"/>; on false the caller returns its own default,
    /// and the warning has already been logged with its own fallback wording.
    /// </summary>
    internal static bool TryLoad<T>(
        ILog log,
        string file,
        string name,
        string fallback,
        Func<T, bool> isValid,
        [NotNullWhen(true)] out T? table,
        JsonSerializerOptions? options = null)
        where T : class
    {
        JsonReadStatus status = JsonFile.TryRead(file, out table, out string? error, options);
        if (status == JsonReadStatus.Missing)
        {
            log.LogWarning($"{name} missing at {file}; {fallback}");
            return false;
        }

        if (table is null || !isValid(table))
        {
            log.LogWarning($"{name} {JsonFile.Describe(error)} at {file}; {fallback}");
            return false;
        }

        return true;
    }
}
