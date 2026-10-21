using System.Globalization;
using System.Windows.Data;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.Views;

/// <summary>
/// Pack-URI string to cached <see cref="System.Windows.Media.Imaging.BitmapSource"/>:
/// <c>Image.Source</c> does not decode a bound resource string through the
/// WIC-safe path on its own, so the view maps it here. The decode and its
/// cache live in <see cref="PackImageSource"/> (Services); the composition
/// root wires that in once through <see cref="Source"/>, keeping the view
/// free of I/O and of a render-thread crash on codec-less machines.
/// </summary>
public sealed class PackImageConverter : IValueConverter
{
    /// <summary>
    /// The image loader, set once by the composition root when it wires the
    /// resource declared in App.xaml. Null until then (design time: no image).
    /// </summary>
    public PackImageSource? Source { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string resource && Source is not null ? Source.Load(resource) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
