using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One job leg pinned on the HUD (taxi pickup/drop-off, a delivery hub pickup
/// or store drop-off, or a Touristenfahrten stop): the door to reach plus the
/// resource keys the HUD formats (drive-to, arrival, action). Copy stays keys
/// and values, never sentences, so a language switch repaints the line like the
/// rest of the companion. <see cref="DriveArgs"/>/<see cref="ArrivedArgs"/> let
/// a leg carry its own placeholders (the Touristenfahrten order and dropoff
/// list); null keeps the shared four-address shape every other leg uses.
///
/// A deadline line (speed challenge or a timed passive ask) rides the same
/// leg: <see cref="DeadlineKey"/> names its countdown copy, so the host decides
/// the wording while the HUD keeps the one clock.
/// </summary>
public sealed record HudJobGuidance(
    string TargetName,
    AddressLocation Target,
    string DriveKey,
    string ArrivedKey,
    string ActionKey,
    double ArrivalRadiusMeters,
    DateTime? Deadline = null,
    string DeadlineKey = "HudSpeedChallengeLeft",
    IReadOnlyList<string>? DriveArgs = null,
    IReadOnlyList<string>? ArrivedArgs = null);
