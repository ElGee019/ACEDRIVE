using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AceDrive.Overlay.Views;

/// <summary>
/// Bool to Visible/Collapsed, inverted: true collapses, false shows. The assign
/// page uses it for its quiet-day line, which hides once the day has requests,
/// so the card never paints both at once.
/// </summary>
public sealed class InverseBoolToVisConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
