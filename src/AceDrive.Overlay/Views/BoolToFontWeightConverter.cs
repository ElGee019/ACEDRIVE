using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AceDrive.Overlay.Views;

/// <summary>Today highlight for the calendar strip: bold when today, normal otherwise.</summary>
public sealed class BoolToFontWeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? FontWeights.Bold : FontWeights.Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
