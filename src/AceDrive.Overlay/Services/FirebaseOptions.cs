using System.Globalization;
using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The public Firebase client configuration: project id, web API key and
/// optional storage bucket. Read from %APPDATA%\AceDrive\firebase.json, with
/// EVO_FIREBASE_PROJECT / EVO_FIREBASE_API_KEY / EVO_FIREBASE_BUCKET as
/// overrides. The key is a client key by design, but it still never enters
/// the repository: no config means the online layer stays off.
/// </summary>
public sealed record FirebaseOptions(string ProjectId, string ApiKey, string StorageBucket)
{
    private const string ProjectVariable = "EVO_FIREBASE_PROJECT";
    private const string ApiKeyVariable = "EVO_FIREBASE_API_KEY";
    private const string BucketVariable = "EVO_FIREBASE_BUCKET";

    /// <summary>
    /// Loads the configuration, or null when it is absent/incomplete. A
    /// missing file is the normal offline state and logs at info level; a
    /// present but broken file logs a warning instead.
    /// </summary>
    public static FirebaseOptions? TryLoad(ILog log)
    {
        string? project = Environment.GetEnvironmentVariable(ProjectVariable);
        string? apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);
        string? bucket = Environment.GetEnvironmentVariable(BucketVariable);

        string file = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "firebase.json");
        if (File.Exists(file))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
                JsonElement root = doc.RootElement;
                project = FirstNonEmpty(project, ReadString(root, "projectId"));
                apiKey = FirstNonEmpty(apiKey, ReadString(root, "apiKey"));
                bucket = FirstNonEmpty(bucket, ReadString(root, "storageBucket"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                log.LogWarning($"firebase: cannot read {file}: {ex.GetType().Name}");
            }
        }

        if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(apiKey))
        {
            log.LogInfo("firebase: no config; online layer off");
            return null;
        }

        string storage = string.IsNullOrWhiteSpace(bucket)
            ? project.Trim() + ".appspot.com"
            : bucket.Trim();
        log.LogInfo($"firebase: project {project.Trim()}, storage {storage}");
        return new FirebaseOptions(project.Trim(), apiKey.Trim(), storage);
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? FirstNonEmpty(string? first, string? second) =>
        string.IsNullOrWhiteSpace(first) ? second : first;

    /// <summary>Invariant timestamp used by every Firestore field.</summary>
    public static string Timestamp(DateTime utc) =>
        utc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
}
