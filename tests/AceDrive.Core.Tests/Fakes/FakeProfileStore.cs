using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeProfileStore : IProfileStore
{
    public PlayerProfile? Profile { get; private set; }

    public FakeProfileStore(PlayerProfile? profile = null) => Profile = profile;

    public PlayerProfile? Load() => Profile;
    public void Save(PlayerProfile profile) => Profile = profile;
}
