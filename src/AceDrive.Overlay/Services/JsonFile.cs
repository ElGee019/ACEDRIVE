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
/// the never-throw guard, the case-insensitive options, the atomic write, the
/// last-good backup and the directory creation are written once instead of
/// once per store (34 copies before this existed). Policy stays in the store
/// that owns the file: what a missing file means, which rows to drop, what to
/// log.
/// </summary>
internal static class JsonFile
{
    /// <summary>
    /// The previous good save, rewritten beside the file on every write. A
    /// save that is truncated by a crash or a lock must never cost the career
    /// when exactly that state is still sitting next to it.
    /// </summary>
    internal const string BackupSuffix = ".bak";

    /// <summary>The half-written next save; never read, deleted on a reset.</summary>
    internal const string TempSuffix = ".tmp";

    // Case-insensitive read: a hand-edited save whose casing drifted must load,
    // not silently deserialize to nulls. Shared so no store can forget it.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Reads and deserialises one file, falling back to <see cref="BackupSuffix"/>
    /// when the file itself is missing, locked or malformed. Never throws: a
    /// file that cannot be read either way reports
    /// <see cref="JsonReadStatus.Unreadable"/> with the exception type in
    /// <paramref name="error"/>, one that exists nowhere reports
    /// <see cref="JsonReadStatus.Missing"/>. <paramref name="options"/> is for
    /// the rare table that needs a converter; everything else shares the
    /// case-insensitive defaults.
    /// </summary>
    internal static JsonReadStatus TryRead<T>(
        string file, out T? value, out string? error,
        JsonSerializerOptions? options = null)
        where T : class =>
        TryRead(file, out value, out error, out _, options);

    /// <summary>
    /// The same read, reporting whether the value came from the backup rather
    /// than from the file itself. A store that wants to say "resumed the last
    /// good save" reads this overload; the rest keep the plain one.
    /// </summary>
    internal static JsonReadStatus TryRead<T>(
        string file, out T? value, out string? error, out bool recovered,
        JsonSerializerOptions? options = null)
        where T : class
    {
        recovered = false;
        JsonReadStatus status = TryReadFile(file, out value, out error, options);
        if (status == JsonReadStatus.Ok)
        {
            return status;
        }

        // WHY the backup: the file is somebody's career. A truncation from a
        // half-finished write, a lock held by a backup tool or an accidental
        // delete must not rewind the player to the first page while the write
        // path has kept a good copy of that very file beside it.
        if (TryReadFile(file + BackupSuffix, out T? previous, out _, options)
            == JsonReadStatus.Ok)
        {
            value = previous;
            error = null;
            recovered = true;
            return JsonReadStatus.Ok;
        }

        return status;
    }

    // The bare read of one path, shared by the file and its backup so both get
    // the same guard and options.
    private static JsonReadStatus TryReadFile<T>(
        string file, out T? value, out string? error,
        JsonSerializerOptions? options)
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
    /// Writes one file atomically, keeping the file it replaces as
    /// <see cref="BackupSuffix"/>. Creates the directory. Returns false when the
    /// write failed (locked file, no permission) with the exception type in
    /// <paramref name="error"/>; never throws, because a failed bank must not
    /// break gameplay or shutdown.
    /// </summary>
    internal static bool TryWrite<T>(string file, T value, out string? error)
    {
        error = null;
        string temp = file + TempSuffix;
        try
        {
            string? dir = Path.GetDirectoryName(file);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }

            // Serialise into the temp file first: a crash or a full disk then
            // leaves the previous save intact instead of a truncated one.
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
            Commit(temp, file);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.GetType().Name;
            // A temp file is never read; a failed write must not leave litter
            // behind for the next reset to report.
            TryDelete(temp);
            return false;
        }
    }

    // WHY File.Replace: it makes the new save visible in one step and keeps the
    // replaced file as the backup in that same step, so a crash leaves either
    // the old or the new state — never half of each. The copy and the move are
    // the tier for a filter that denies the rename but allows the write: the
    // save must still land, and the backup guarantee must not depend on the
    // filesystem's Replace support.
    private static void Commit(string temp, string file)
    {
        if (File.Exists(file))
        {
            string backup = file + BackupSuffix;
            // A stale backup would make Replace fail on a filter that refuses
            // to overwrite it; the primary is still the newest-good state.
            TryDelete(backup);
            try
            {
                File.Replace(temp, file, backup);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Replace denied (a lock, a non-NTFS volume, an AV filter):
                // keep the same backup guarantee with a plain copy, then let
                // the move below land the new save.
                TryCopy(file, backup);
            }
        }

        try
        {
            File.Move(temp, file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Last resort: the filter denied every rename. This is the one
            // path that can leave a truncated file if the process dies mid-way,
            // and the reason the read path keeps the backup.
            File.WriteAllText(file, File.ReadAllText(temp));
            TryDelete(temp);
        }
    }

    private static void TryCopy(string source, string target)
    {
        try
        {
            File.Copy(source, target, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // WHY ignored: a save without a backup is still better than no
            // save; the failure is the filesystem's, not the player's.
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        catch (IOException)
        {
            // WHY: best-effort cleanup of a file nobody reads; failing to remove
            // litter must not turn into a failed save.
        }
        catch (UnauthorizedAccessException)
        {
            // Same best-effort rule for a file a filter refuses to remove.
        }
    }
}
