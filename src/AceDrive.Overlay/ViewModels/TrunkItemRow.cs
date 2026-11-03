namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One carried stack as the trunk page lists it (Unity YouScreen's item row):
/// the name, the quantity and the hunger a bite would remove, all already
/// formatted. The same row shape serves both halves of the page — <c>CanEat</c>
/// is the page's food test: a food stack shows its hunger value and its EAT tap,
/// a glove-compartment stack (an award, a certificate) shows neither, because
/// the catalogue defines no bite for it and the page must not invent one. A
/// counter-limited reward card also prints its remaining charges, which is the
/// countdown a completed job spends.
/// </summary>
public sealed record TrunkItemRow(
    string Name,
    string QuantityLine,
    string HungerLine,
    string UsesLine,
    bool CanEat,
    string DescriptionLine = "")
{
    /// <summary>True when the row carries a countdown to show.</summary>
    public bool HasUses => UsesLine.Length > 0;

    /// <summary>True when the row has a reward card's function text.</summary>
    public bool HasDescription => DescriptionLine.Length > 0;
}
