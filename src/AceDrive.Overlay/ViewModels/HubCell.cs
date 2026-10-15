namespace AceDrive.Overlay.ViewModels;

/// <summary>One hub inside a career column (a Unity career button),
/// preformatted: owned hubs read as just the name, priced ones as
/// "{name} - {cost}CredP". LabelLine stays the voice key and whole-cell
/// text; NameLine/PriceLine are the same sentence split for the view,
/// which paints the price white/grey by affordability. The view owns the
/// paints.</summary>
public sealed record HubCell(
    int CareerIndex,
    int LevelIndex,
    string LabelLine,
    string NameLine,
    string PriceLine,
    bool IsOwned,
    bool IsAffordable,
    bool IsSelected)
{
    /// <summary>Unity interactable rule: owned and unaffordable hubs do not
    /// pick; only the selection then enables OPEN CAREER.</summary>
    public bool IsSelectable => !IsOwned && IsAffordable;
}
