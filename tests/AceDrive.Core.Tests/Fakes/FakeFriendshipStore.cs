using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeFriendshipStore : IFriendshipStore
{
    private readonly Dictionary<string, int> _levels = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Names => _levels.Keys;

    public int GetLevel(string ownerName) =>
        _levels.GetValueOrDefault(((ownerName ?? "").Trim()));

    public FriendshipOutcome RegisterRide(string ownerName) => Apply(ownerName, 1);

    public void AddFamiliarity(string ownerName, int delta) => _ = Apply(ownerName, delta);

    private FriendshipOutcome Apply(string ownerName, int delta)
    {
        string raw = (ownerName ?? "").Trim();
        string key = raw;
        if (key.Length == 0)
        {
            return new FriendshipOutcome("", 0, false);
        }
        int before = _levels.GetValueOrDefault(key);
        int after = Math.Max(0, before + delta);
        _levels[key] = after;
        return new FriendshipOutcome(raw, after, FriendshipLevels.TierIncreased(before, after));
    }
}
