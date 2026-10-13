using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AceDrive.Core;

namespace AceDrive.Overlay.Views;

/// <summary>
/// Stock band to fill color, the Unity StoreListView palette: red under
/// 25 %, amber under 60 %, green above. Core returns the band as data;
/// the paints stay here (SRP).
/// </summary>
public sealed class StockLevelToBrushConverter : IValueConverter
{
    private static readonly Brush Red = Frozen(0xE5, 0x39, 0x35);
    private static readonly Brush Amber = Frozen(0xFB, 0x8C, 0x00);
    private static readonly Brush Green = Frozen(0x43, 0xA0, 0x47);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is StockLevel level ? level switch
        {
            StockLevel.Red => Red,
            StockLevel.Amber => Amber,
            _ => Green,
        } : Green;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    // Frozen brushes cross threads safely and skip change-notification cost.
    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
