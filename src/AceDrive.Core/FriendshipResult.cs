// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Data half of one familiarity change: the store owns the levels, the
// receipt only carries the outcome to render.
namespace AceDrive.Core;

/// <summary>One settled familiarity change, ready to render.</summary>
public sealed record FriendshipResult(string OwnerName, int Level, bool TierIncreased);
