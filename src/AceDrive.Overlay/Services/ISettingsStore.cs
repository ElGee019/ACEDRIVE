namespace AceDrive.Overlay.Services;

public sealed record WindowBounds(double Left, double Top, double Width, double Height)
{
    public bool IsSane => Width > 200 && Height > 200;
}

/// <summary>Persistence behind an interface so views never touch the disk.</summary>
public interface ISettingsStore
{
    WindowBounds? Load();
    void Save(WindowBounds bounds);
}
