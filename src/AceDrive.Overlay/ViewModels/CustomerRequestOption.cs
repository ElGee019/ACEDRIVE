using System.Windows.Input;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One tappable row on the customer-request page: the formatted option text,
/// the button's own label and the action that grades it. Quiz rows print a
/// number word on the button (ONE/TWO/THREE, Unity DeliveryJobScreen's store
/// numbering) and read the value beside it; the passive ask's ACCEPT / DECLINE
/// rows carry their label on the button itself.
/// </summary>
public sealed record CustomerRequestOption(
    string Text, string ButtonText, ICommand Command)
{
    /// <summary>True when the row prints its value beside the button instead
    /// of on it: the three quiz answers, never ACCEPT / DECLINE.</summary>
    public bool HasReadout =>
        !string.Equals(Text, ButtonText, StringComparison.Ordinal);
}
