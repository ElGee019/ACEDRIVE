// Data half of one familiarity change. Lives next to the interface that
// owns it (single accepted co-location): the Core record of the same shape
// stays dependency-free for a future Lua port, this one feeds the store.
namespace AceDrive.Overlay.Services;

/// <summary>Outcome of one familiarity change, ready for the receipt.</summary>
public sealed record FriendshipOutcome(string OwnerName, int Level, bool TierIncreased);
