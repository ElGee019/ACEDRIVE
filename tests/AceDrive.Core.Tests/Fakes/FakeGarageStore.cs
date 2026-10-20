using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>In-memory garage store: a fake substitutes for JsonGarageStore without the caller knowing (LSP).</summary>
public sealed class FakeGarageStore : IGarageStore
{
    private GarageState? _state;

    public int Saves { get; private set; }

    public GarageState? Load() => _state;

    public void Save(GarageState state)
    {
        _state = state;
        Saves++;
    }
}
