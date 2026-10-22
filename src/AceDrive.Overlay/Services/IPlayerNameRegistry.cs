namespace AceDrive.Overlay.Services;

/// <summary>
/// Global display-name reservation. Small on purpose (ISP): the onboarding
/// page only asks whether a name can be claimed; normalization, auth and
/// Firestore stay inside the implementation.
/// </summary>
public interface IPlayerNameRegistry
{
    /// <summary>Claims the name, or reports why it cannot be claimed.</summary>
    Task<NameClaimResult> ClaimAsync(
        string displayName, CancellationToken cancellationToken = default);
}
