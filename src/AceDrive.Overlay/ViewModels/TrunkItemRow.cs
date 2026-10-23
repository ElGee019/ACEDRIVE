namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One carried stack as the trunk page lists it (Unity YouScreen's item row):
/// the name, the quantity and the hunger a bite would remove, all already
/// formatted. A stack the catalogue no longer defines keeps its name but
/// cannot be eaten, so the row's EAT tap disables instead of guessing a value.
/// </summary>
public sealed record TrunkItemRow(
    string Name,
    string QuantityLine,
    string HungerLine,
    bool CanEat);
