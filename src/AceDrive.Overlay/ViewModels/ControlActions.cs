using System.Windows.Input;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The control page's door commands, grouped so the page constructor keeps
/// one parameter per concern instead of one per button. A new door extends
/// this record (and the page's bindings), never the page's signature.
/// </summary>
public sealed record ControlActions(
    ICommand Taxi,
    ICommand Delivery,
    ICommand Haul,
    ICommand Bank,
    ICommand Garage,
    ICommand Dealer,
    ICommand CarWash,
    ICommand Mechanic,
    ICommand Insurance);
