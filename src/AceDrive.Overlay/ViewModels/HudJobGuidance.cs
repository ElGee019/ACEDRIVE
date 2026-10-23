using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One taxi leg pinned on the HUD: the door to reach plus the resource keys
/// the HUD formats (drive-to, arrival, action). Copy stays keys and values,
/// never sentences, so a language switch repaints the line like the rest of
/// the companion. The host owns when the leg changes; the HUD only resolves
/// distance and the parked-at-the-door state.
/// </summary>
public sealed record HudJobGuidance(
    string TargetName,
    AddressLocation Target,
    string DriveKey,
    string ArrivedKey,
    string ActionKey,
    double ArrivalRadiusMeters,
    DateTime? SpeedChallengeDeadline = null);
