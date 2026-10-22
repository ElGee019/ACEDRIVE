using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>In-memory refresh-token store for session tests.</summary>
public sealed class FakeSessionStore : IFirebaseSessionStore
{
    public FirebaseSessionState? State { get; set; }
    public int Clears { get; private set; }

    public FirebaseSessionState? Load() => State;
    public void Save(FirebaseSessionState session) => State = session;

    public void Clear()
    {
        Clears++;
        State = null;
    }
}
