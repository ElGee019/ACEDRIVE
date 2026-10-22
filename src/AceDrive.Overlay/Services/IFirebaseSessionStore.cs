namespace AceDrive.Overlay.Services;

/// <summary>
/// Local persistence of the anonymous refresh token. Without it every launch
/// would mint a new uid and leave an orphan player doc on the world boards.
/// </summary>
public interface IFirebaseSessionStore
{
    FirebaseSessionState? Load();
    void Save(FirebaseSessionState session);
    void Clear();
}
