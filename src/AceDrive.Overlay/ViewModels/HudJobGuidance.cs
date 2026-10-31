using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One job leg pinned on the HUD (taxi pickup/drop-off, a delivery hub pickup
/// or store drop-off, or a Touristenfahrten stop): the door to reach plus the
/// resource keys the HUD formats (drive-to, arrival, action). Copy stays keys
/// and values, never sentences, so a language switch repaints the line like the
/// rest of the companion. <see cref="DriveArgs"/> carries the leg's own values
/// <em>after</em> the shared address shape (name, street, number, city), so a
/// drive line always names where it sends the player and then what it is for
/// (the Touristenfahrten order or dropoff list); an empty list is the shared
/// four alone, like every other leg.
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
