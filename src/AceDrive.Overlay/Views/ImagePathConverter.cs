using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace AceDrive.Overlay.Views;

/// <summary>
/// Path string to cached BitmapImage: Image.Source does not convert bound
/// strings on its own, so the view maps them here. DecodeOnce + freeze keeps
/// one instance per file on the UI thread. Missing/unreadable = no image,
/// never an exception.
/// </summary>
public sealed class ImagePathConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, BitmapImage?> Cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string path ? Cache.GetOrAdd(path, Load) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static BitmapImage? Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
