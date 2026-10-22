namespace AceDrive.Overlay.Services;

/// <summary>The persisted anonymous Firebase identity.</summary>
public sealed record FirebaseSessionState(string Uid, string RefreshToken);
