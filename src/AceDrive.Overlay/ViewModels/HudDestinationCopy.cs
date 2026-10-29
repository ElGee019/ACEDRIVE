namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Story-authored resource keys for one HUD destination leg. All three keep
/// the generic drive-to placeholders ({0} name, {1} street, {2} number,
/// {3} city) so a translation can reorder them; null members fall back to
/// the HUD's default line and action for the POI type, and an empty
/// <c>ArrivedKey</c> keeps the arrival line down on purpose (a leg whose
/// arrival is nothing but its action button).
///
/// <see cref="DriveArgs"/>/<see cref="ArrivedArgs"/> replace those shared
/// placeholders with the leg's own values (the Touristenfahrten order and
/// dropoff list), so a sentence shaped differently still keeps its copy in
/// resx.
/// </summary>
public sealed record HudDestinationCopy(
    string DriveKey, string? ArrivedKey = null, string? ActionKey = null,
    IReadOnlyList<string>? DriveArgs = null, IReadOnlyList<string>? ArrivedArgs = null);
