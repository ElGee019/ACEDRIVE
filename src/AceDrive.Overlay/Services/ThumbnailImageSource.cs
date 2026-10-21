using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Decodes a thumbnail file once and keeps the frozen bitmap, so the view
/// layer maps a path to an image without touching the filesystem (AGENTS.md:
/// views draw, services own I/O). Missing or unreadable files answer null and
/// never throw; the cache is per instance, wired once at startup.
/// </summary>
public sealed class ThumbnailImageSource
{
    private readonly ConcurrentDictionary<string, BitmapImage?> _cache = new();

    /// <summary>
    /// The decoded bitmap for this path, or null when the file is missing or
    /// unreadable. Decoded lazily and cached; a repeat call is a dictionary
    /// hit, so binding stays cheap.
    /// </summary>
    public BitmapImage? Load(string path) => _cache.GetOrAdd(path, Decode);

    private static BitmapImage? Decode(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or NotSupportedException or UriFormatException or ArgumentException)
        {
            // Decode/permission/uri failure at the image boundary: a missing
            // file or a codec-less machine shows no thumbnail instead of an
            // exception (degrade gracefully, AGENTS.md).
            return null;
        }
    }
}
