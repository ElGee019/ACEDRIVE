using System.IO;

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
        : this(DataPaths.LocalSave("firebase_session.json"))
    { }

    internal JsonFirebaseSessionStore(string file) => _file = file;

    public FirebaseSessionState? Load()
    {
        JsonFile.TryRead(_file, out FirebaseSessionState? state, out _);
        return state is { Uid.Length: > 0, RefreshToken.Length: > 0 } ? state : null;
    }

    public void Save(FirebaseSessionState session)
    {
        // A read-only profile still plays offline; the next launch mints a
        // fresh anonymous uid, which is a harmless board entry.
        JsonFile.TryWrite(_file, session, out _);
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
