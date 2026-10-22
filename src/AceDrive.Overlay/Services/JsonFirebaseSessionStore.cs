using System.IO;
using System.Text.Json;

namespace AceDrive.Overlay.Services;

/// <summary>
/// JSON session file under %LOCALAPPDATA%\AceDrive. The refresh token is a
/// credential, so it stays on this machine and never in the repository.
/// Never throws: a corrupt/locked file just means a fresh anonymous login.
/// </summary>
public sealed class JsonFirebaseSessionStore : IFirebaseSessionStore
{
    private readonly string _file;

    public JsonFirebaseSessionStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AceDrive", "firebase_session.json"))
    { }

    internal JsonFirebaseSessionStore(string file) => _file = file;

    public FirebaseSessionState? Load()
    {
        try
        {
            if (!File.Exists(_file)) return null;
            FirebaseSessionState? state =
                JsonSerializer.Deserialize<FirebaseSessionState>(File.ReadAllText(_file));
            return state is { Uid.Length: > 0, RefreshToken.Length: > 0 } ? state : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Save(FirebaseSessionState session)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(session));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A read-only profile still plays offline; the next launch mints a
            // fresh anonymous uid, which is a harmless board entry.
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_file)) File.Delete(_file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Same as Save: losing the clear only costs an orphaned uid later.
        }
    }
}
