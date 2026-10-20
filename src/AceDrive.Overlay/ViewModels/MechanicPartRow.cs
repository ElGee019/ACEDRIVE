namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One part row on the mechanic page (Unity CarMechanicScreen's cloned
/// PartsPrefab row): the part's name, its efficiency reading, the class-scaled
/// replacement price and the ADD button's label/state. The queued flag drives
/// the row's highlight and hides the button (Unity's add-only queue: once ADDed
/// the button is gone). Preformatted copy, so the view binds text only.
/// </summary>
public sealed record MechanicPartRow(
    string Key,
    string Name,
    string StatusText,
    string PriceText,
    string ActionLabel,
    bool AddEnabled,
    bool AddVisible,
    bool Queued);
