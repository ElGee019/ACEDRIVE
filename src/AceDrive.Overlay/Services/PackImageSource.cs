using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Decodes an embedded (pack URI) artwork resource once and keeps the frozen
/// bitmap, so the view layer maps a resource to an image without touching
/// I/O. A missing resource, or a machine whose WIC has no codec for the
/// format (WebP ships with Win11, not every Win10), answers null and never
/// throws; the view then simply omits that artwork. The cache is per instance,
/// wired once at startup like <see cref="ThumbnailImageSource"/>.
/// </summary>
public sealed class PackImageSource
{
    private readonly ConcurrentDictionary<string, BitmapSource?> _cache = new();

    /// <summary>
    /// The decoded bitmap for this pack URI, or null when the resource is
    /// missing or undecodable. Decoded lazily and cached; a repeat call is a
    /// dictionary hit, so binding stays cheap.
    /// </summary>
    public BitmapSource? Load(string resource) => _cache.GetOrAdd(resource, Decode);

    private static BitmapSource? Decode(string resource)
    {
        try
        {
            // WHY the extra conversion: WPF reports a WebP frame as Bgr32 and
            // silently drops its alpha (dotnet/wpf#1436), which would square
            // off the tiles and paint their transparent label band. WIC keeps
            // the alpha; forcing Bgra32 reads it back.
            var decoder = BitmapDecoder.Create(
                new Uri(resource, UriKind.Absolute),
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            var image = new FormatConvertedBitmap(
                decoder.Frames[0], PixelFormats.Bgra32, null, 0);
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException
            or UriFormatException or ArgumentException)
        {
            // Decode/codec/uri failure at the artwork boundary: a build without
            // the resource or a codec-less WIC shows no tile instead of taking
            // the render thread down (degrade gracefully, AGENTS.md).
            return null;
        }
    }
}
