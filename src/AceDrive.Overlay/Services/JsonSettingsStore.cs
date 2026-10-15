using System.IO;
using System.Text.Json;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.Services;

/// <summary>JSON settings under %AppData%\AceDrive. Never throws.</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _file;

    public JsonSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "window.json"))
    { }

    internal JsonSettingsStore(string file) => _file = file;

    public WindowBounds? Load()
    {
        try
        {
            if (!File.Exists(_file)) return null;
            var b = JsonSerializer.Deserialize<WindowBounds>(File.ReadAllText(_file));
            return b is { IsSane: true } ? b : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Corrupt or locked settings just mean "no saved placement":
            // the window falls back to defaults instead of blocking startup.
            return null;
        }
    }

    public void Save(WindowBounds bounds)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(bounds));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // never break shutdown for persistence
        }
    }
}
