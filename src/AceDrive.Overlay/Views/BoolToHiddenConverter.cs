using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AceDrive.Overlay.Views;

/// <summary>
/// Bool to Visible/Hidden: like the shared BooleanToVisibilityConverter but
/// the false branch hides instead of collapsing, so the row keeps its space.
/// Only the receipt's fine row uses it: the fine is gone for now, but its
/// empty line stays to hold the layout steady.
/// </summary>
public sealed class BoolToHiddenConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Hidden;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
