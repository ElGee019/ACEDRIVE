namespace AceDrive.Overlay.Services;

/// <summary>JSON settings under %AppData%\AceDrive. Never throws.</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _file;

    public JsonSettingsStore()
        : this(DataPaths.UserSave("window.json"))
    { }

    internal JsonSettingsStore(string file) => _file = file;

    public WindowBounds? Load()
    {
        // Corrupt, locked or absent settings all mean "no saved placement":
        // the window falls back to defaults instead of blocking startup.
        JsonFile.TryRead(_file, out WindowBounds? bounds, out _);
        return bounds is { IsSane: true } ? bounds : null;
    }

    public void Save(WindowBounds bounds)
    {
        // never break shutdown for persistence
        JsonFile.TryWrite(_file, bounds, out _);
    }
}
