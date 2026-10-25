using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One Cento Miglia tier button, preformatted like the calendar's rows: the
/// label the mic and the button print (the era tier's decade already formatted
/// in) and the requirement line under it. Locked rows stay visible — Unity keeps
/// them on the page and only drops the interactable flag.
/// </summary>
public sealed record CentoTierRow(
    CentoTier Tier, string Label, string Requirement, bool Eligible);
