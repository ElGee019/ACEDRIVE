namespace AceDrive.Overlay.ViewModels;

/// <summary>A tier button above the columns. Unity rule: disabled tiers
/// stay clickable and just show COMING SOON columns.</summary>
public sealed record HubTierCell(int Index, string LabelLine, bool IsActive);
