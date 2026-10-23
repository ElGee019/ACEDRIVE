using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AceDrive.Overlay.Views;

/// <summary>
/// Battery fill fraction (0..1) to the clip that reveals the gradient from the
/// bottom up, one meter step at a time: the mask's top edge sits at the filled
/// height and everything above it is cut away, so the unlit blocks keep the
/// artwork's empty look. The parameter is the fill layer's art size
/// ("61,118"), so the geometry lives in the view; the clip is in the image's
/// local space, and the tile's Viewbox scales it with the artwork.
/// </summary>
public sealed class MeterFillConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        (double width, double height) = Size(parameter as string);
        double fill = value is double fraction ? Math.Clamp(fraction, 0.0, 1.0) : 0.0;
        var geometry = new RectangleGeometry(
            new Rect(0.0, height - (height * fill), width, height * fill));
        geometry.Freeze();
        return geometry;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    // A malformed size cuts the whole fill away instead of guessing a box: the
    // tile reads empty rather than painting the wrong window. The converter is
    // wired once in XAML, so this only fires on a typo.
    private static (double Width, double Height) Size(string? size)
    {
        string[] parts = (size ?? "").Split(',');
        return parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double width)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double height)
            ? (width, height)
            : (0.0, 0.0);
    }
}
