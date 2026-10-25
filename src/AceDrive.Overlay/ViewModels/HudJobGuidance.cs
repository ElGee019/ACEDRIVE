using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One job leg pinned on the HUD (taxi pickup/drop-off or a delivery hub
/// pickup/store drop-off): the door to reach plus the resource keys the HUD
/// formats (drive-to, arrival, action). Copy stays keys and values, never
/// sentences, so a language switch repaints the line like the rest of the
/// companion. The host owns when the leg changes; the HUD only resolves
/// distance and the parked-at-the-door state.
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
    string DeadlineKey = "HudSpeedChallengeLeft");
