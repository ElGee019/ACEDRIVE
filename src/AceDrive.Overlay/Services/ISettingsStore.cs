namespace AceDrive.Overlay.Services;

public sealed record WindowBounds(
    double Left, double Top, double Width, double Height, int Version = 0)
{
    // Both windows are user-placed: the main panel can shrink to 80 px high,
    // the companion HUD's driving strip is 200 px (a customer request grows
    // the frame while it is up). Reject only finite, implausibly small boxes,
    // never a legitimate HUD strip.
    public bool IsSane =>
        double.IsFinite(Left) && double.IsFinite(Top)
        && double.IsFinite(Width) && double.IsFinite(Height)
        && Width > 100.0 && Height >= 80.0;

    // Placement schema version. Files written before the home page defined
    // the panel's standard size carry no Version (0); the main window grows
    // those once to the standard height instead of restoring a size that
    // scrolls at standard proportions. The HUD stores this field too but
    // never reads it: its strip size is fixed.
    public const int CurrentVersion = 1;
}

/// <summary>Persistence behind an interface so views never touch the disk.</summary>
public interface ISettingsStore
{
    WindowBounds? Load();
    void Save(WindowBounds bounds);
}
