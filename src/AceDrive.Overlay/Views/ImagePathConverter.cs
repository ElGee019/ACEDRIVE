using System.Globalization;
using System.Windows.Data;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.Views;

/// <summary>
/// Path string to cached <see cref="System.Windows.Media.Imaging.BitmapImage"/>:
/// <c>Image.Source</c> does not convert bound strings on its own, so the view
/// maps them here. The decode and its cache live in
/// <see cref="ThumbnailImageSource"/> (Services); the startup wires that in
/// once through <see cref="Source"/>, keeping the view free of file I/O.
/// </summary>
public sealed class ImagePathConverter : IValueConverter
{
    /// <summary>
    /// The image loader, set once by the composition root when it wires the
    /// resource declared in App.xaml. Null until then (design time: no image).
    /// </summary>
    public ThumbnailImageSource? Source { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string path && Source is not null ? Source.Load(path) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
