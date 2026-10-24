using System.IO;
using System.Text.Json;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Outcome of one guarded file read: a missing file is a normal first run,
/// while unreadable covers a lock or malformed JSON. The store keeps its own
/// message and fallback for each case, so the distinction stays visible in the
/// log instead of collapsing into one "something went wrong".
/// </summary>
internal enum JsonReadStatus
{
    Ok,
    Missing,
    Unreadable,
}

/// <summary>
/// The one JSON file boundary. Every store reads and writes through here, so
/// the never-throw guard, the case-insensitive options and the directory
/// creation are written once instead of once per store (34 copies before this
/// existed). Policy stays in the store that owns the file: what a missing file
/// means, which rows to drop, what to log.
/// </summary>
internal static class JsonFile
{
    // Case-insensitive read: a hand-edited save whose casing drifted must load,
    // not silently deserialize to nulls. Shared so no store can forget it.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Reads and deserialises one file. Never throws: a locked or malformed
    /// file reports <see cref="JsonReadStatus.Unreadable"/> with the exception
    /// type in <paramref name="error"/>, a missing one reports
    /// <see cref="JsonReadStatus.Missing"/>. <paramref name="options"/> is for
    /// the rare table that needs a converter; everything else shares the
    /// case-insensitive defaults.
    /// </summary>
    internal static JsonReadStatus TryRead<T>(
        string file, out T? value, out string? error,
        JsonSerializerOptions? options = null)
        where T : class
    {
        value = null;
        error = null;
        try
        {
            if (!File.Exists(file))
            {
                return JsonReadStatus.Missing;
            }

            value = JsonSerializer.Deserialize<T>(
                File.ReadAllText(file), options ?? Options);
            return JsonReadStatus.Ok;
        }
        // WHY the guarded set: a corrupt or locked data file must never take the
        // overlay down (AGENTS.md: Try* at every external boundary). Anything
        // else stays a crash.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            error = ex.GetType().Name;
            return JsonReadStatus.Unreadable;
        }
    }

    /// <summary>
    /// One wording for "the file is there but unusable": a lock or malformed
    /// JSON names its exception, a failed schema check reads "invalid".
    /// </summary>
    internal static string Describe(string? error) =>
        error is null ? "invalid" : $"unreadable ({error})";

    /// <summary>
    /// Writes one file, creating its directory. Returns false when the write
    /// failed (locked file, no permission) with the exception type in
    /// <paramref name="error"/>; never throws, because a failed bank must not
    /// break gameplay or shutdown.
    /// </summary>
    internal static bool TryWrite<T>(string file, T value, out string? error)
    {
        error = null;
        try
        {
            string? dir = Path.GetDirectoryName(file);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(file, JsonSerializer.Serialize(value, Options));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.GetType().Name;
            return false;
        }
    }
}
