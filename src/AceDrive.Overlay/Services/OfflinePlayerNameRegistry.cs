namespace AceDrive.Overlay.Services;

/// <summary>
/// Null-object name registry: offline single-player never blocks on a name
/// reservation, exactly like the Unity game when Firebase is not configured.
/// </summary>
public sealed class OfflinePlayerNameRegistry : IPlayerNameRegistry
{
    public Task<NameClaimResult> ClaimAsync(
        string displayName, CancellationToken cancellationToken = default) =>
        Task.FromResult(NameClaimResult.Unavailable);
}
