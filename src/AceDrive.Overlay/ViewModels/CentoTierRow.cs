using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One Cento Miglia tier button, preformatted like the calendar's rows: the
/// label the mic and the button print (the era tier's decade already formatted
/// in), the requirement line under it and the tier's 1-based place in the table
/// (Unity's event index, which decides the registration number's digit count).
/// Locked rows stay visible — Unity keeps them on the page and only drops the
/// interactable flag.
/// </summary>
public sealed record CentoTierRow(
    CentoTier Tier, string Label, string Requirement, bool Eligible, int Number);
