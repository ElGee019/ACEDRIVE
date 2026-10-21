using System.Windows.Input;

namespace AceDrive.Overlay.ViewModels;

/// <summary>One tappable row on the customer-request page: the formatted
/// option text and the action that grades it. Quiz options format their
/// number with the category's resx unit key; passive/speed rows use the
/// ACCEPT / DECLINE labels.</summary>
public sealed record CustomerRequestOption(string Text, ICommand Command);
