using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeStore : ISettingsStore
{
    public WindowBounds? Saved { get; private set; }
    public WindowBounds? Load() => null;
    public void Save(WindowBounds bounds) => Saved = bounds;
}
