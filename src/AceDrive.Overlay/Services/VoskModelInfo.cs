using System.IO;

namespace AceDrive.Overlay.Services;

/// <summary>
/// One-line identity for the Vosk model folder the voice listener opened.
/// The folder is renamed to a neutral "vosk-model" (the name App.CreateVoice
/// looks for), so the folder alone never says which download is live; the size
/// and the README the model ships do. Logging both turns "voice hears
/// everything wrong" into one glance at the log.
/// </summary>
public static class VoskModelInfo
{
    // The README's opening line is the model's own description ("US English
    // model for mobile Vosk applications"); what follows is licence prose, so
    // only the first non-empty line is read and long ones are cut to keep the
    // log line usable.
    private const int MaxDescriptionChars = 96;

    private const double BytesPerMegabyte = 1024.0 * 1024.0;

    /// <summary>
    /// "vosk-model (67.6 MB) - US English model for mobile Vosk applications",
    /// or just the parts that could be read when the folder or its README is
    /// missing. Never throws: this runs on the voice-start path, whose
    /// contract is to degrade to voice-off, not to fail the overlay.
    /// </summary>
    public static string Describe(string modelPath)
    {
        string name = FolderName(modelPath);
        string size = TryFolderMegabytes(modelPath) is { } megabytes
            ? $"{megabytes:0.0} MB"
            : "size unknown";
        string? description = TryReadmeLine(modelPath);
        return description is null ? $"{name} ({size})" : $"{name} ({size}) - {description}";
    }

    // WHY the folder name may be empty: a configured model path can be the
    // drive root or a bare drive-relative string, and a log line reading
    // " (size unknown)" alone says nothing about which model was attempted.
    private static string FolderName(string modelPath)
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(modelPath));
        return name.Length > 0 ? name : modelPath;
    }

    // WHY no cache: read once per voice start on the cold path, never in the
    // 10 Hz poll; a model folder the player swaps needs no invalidation
    // because the next start re-reads it.
    private static double? TryFolderMegabytes(string modelPath)
    {
        try
        {
            var folder = new DirectoryInfo(modelPath);
            if (!folder.Exists)
            {
                return null;
            }

            long bytes = 0;
            foreach (FileInfo file in folder.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                bytes += file.Length;
            }

            return bytes / BytesPerMegabyte;
        }
        // WHY these two: a half-copied or locked model folder is exactly the
        // state this line exists to expose, and the log must still say what it
        // could read (CA1031: the file-system failure set, not a blanket mute).
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? TryReadmeLine(string modelPath)
    {
        try
        {
            string readme = Path.Combine(modelPath, "README");
            if (!File.Exists(readme))
            {
                return null;
            }

            foreach (string line in File.ReadLines(readme))
            {
                string text = line.Trim();
                if (text.Length > 0)
                {
                    return Clip(text);
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // ASCII ellipsis: the log sink is UTF-8, but a model README is arbitrary
    // third-party text and a mangled byte is not worth the identity line.
    private static string Clip(string text) =>
        text.Length <= MaxDescriptionChars
            ? text
            : text[..MaxDescriptionChars].TrimEnd() + "...";
}
