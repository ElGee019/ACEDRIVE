namespace AceDrive.Overlay.Services;

public sealed record WindowBounds(double Left, double Top, double Width, double Height)
{
    // Both windows are user-placed: the main panel can shrink to 80 px high,
    // the companion HUD is fixed at 200 px. Reject only finite,
    // implausibly small boxes, never a legitimate HUD strip.
    public bool IsSane =>
        double.IsFinite(Left) && double.IsFinite(Top)
        && double.IsFinite(Width) && double.IsFinite(Height)
        && Width > 100.0 && Height >= 80.0;
}

/// <summary>Persistence behind an interface so views never touch the disk.</summary>
public interface ISettingsStore
{
    WindowBounds? Load();
    void Save(WindowBounds bounds);
}
