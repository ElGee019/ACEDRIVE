using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Renders car thumbnail PNGs from the game's own <c>*.texturemips</c> +
/// <c>*.texture</c> sidecar pairs: raw 32 bpp studio shots as the game
/// rendered them — SavedCars renders carry the PLAYER's livery. Decoded,
/// backdrop-cropped and cached as PNG; a cached file is reused until the
/// game rewrites its render (repaints show up on next start). Every failure
/// path returns null and logs — the overlay never breaks over a thumbnail.
/// Ported from the shell test (evo-companion, now removed); one-shot
/// studio-thumbnail provenance preserved there: Data\Thumbs came from
/// content.kspkg via ACEvo.Package.CLI unpack of
/// content\cars\&lt;ks_id&gt;\generated\thumbnails\&lt;preset&gt;.texturemips —
/// those are BC7, 64x64-block tiles row-major (needs deswizzle + WIC DDS
/// decode), unlike the raw-BGRA SavedCars files handled here.
/// </summary>
public sealed class CarThumbnailExtractor
{
    private readonly ILog _log;
    private readonly string _savedCarsThumbsDir;
    private readonly string _profilesDir;

    public CarThumbnailExtractor(
        ILog log, string? savedCarsThumbsDir = null, string? profilesDir = null)
    {
        _log = log;
        string aceDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Saved Games", "ACE");
        _savedCarsThumbsDir = savedCarsThumbsDir
            ?? Path.Combine(aceDir, "SavedCars", "Thumbnails");
        _profilesDir = profilesDir ?? Path.Combine(aceDir, "ProfileData");
    }

    /// <summary>Cache dir: %LocalAppData%\AceDrive\thumbs.</summary>
    public static string DefaultCacheDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AceDrive", "thumbs");

    /// <summary>
    /// The player's garage render for a ks car id: saved-car states in any
    /// profile's OpenData\SavedCars name the car instances (GUIDs) whose
    /// thumbnails the game renders with the player's livery. Newest state
    /// first until a render actually shows a car. Null when that car was
    /// never saved or no render is usable.
    /// </summary>
    public string? TryGetPlayerCarThumb(string ksId, string? cacheDir = null)
    {
        // Ids feed file-name patterns straight away: keep to their alphabet.
        if (ksId.Length == 0
            || !ksId.All(c => char.IsAsciiLetterOrDigit(c) || c is '_')) return null;

        try
        {
            var candidates = new List<(DateTime Written, string State)>();
            foreach (string profile in Directory.EnumerateDirectories(_profilesDir))
            {
                string states = Path.Combine(profile, "OpenData", "SavedCars");
                if (!Directory.Exists(states)) continue;
                foreach (string state in Directory.EnumerateFiles(
                    states, ksId + "_*.carfinalstatewithconsumable"))
                {
                    candidates.Add((File.GetLastWriteTimeUtc(state), state));
                }
            }

            foreach ((_, string state) in candidates.OrderByDescending(c => c.Written))
            {
                string suffix = Path.GetFileNameWithoutExtension(state)
                    [(ksId.Length + 1)..];
                if (!Guid.TryParse(suffix, out Guid carGuid)) continue;

                string src = Path.Combine(
                    _savedCarsThumbsDir, carGuid.ToString("D") + ".texturemips");
                if (!File.Exists(src)) continue;
                string? png = EnsurePng(src, cacheDir ?? DefaultCacheDir);
                if (png is null) continue; // blank/corrupt render: older instance may work
                _log.LogInfo($"thumb {ksId}: player garage livery");
                return png;
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.LogWarning($"thumb {ksId}: {ex.GetType().Name}");
            return null;
        }
    }

    /// <summary>
    /// PNG for one texturemips file, decoding on first use and whenever the
    /// game render is newer than our cache. Null when the input is missing
    /// or unreadable.
    /// </summary>
    public string? EnsurePng(string texturemipsPath, string cacheDir)
    {
        string name = Path.GetFileNameWithoutExtension(texturemipsPath);
        string png = Path.Combine(cacheDir, name + ".png");

        try
        {
            if (File.Exists(png) && !IsStale(png, texturemipsPath)) return png;
            return TryExtract(texturemipsPath, png) ? png : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or NotSupportedException or InvalidOperationException)
        {
            _log.LogWarning($"thumb {name}: {ex.GetType().Name}");
            return File.Exists(png) ? png : null; // stale beats broken
        }
    }

    private static bool IsStale(string png, string source) =>
        File.GetLastWriteTimeUtc(png) < File.GetLastWriteTimeUtc(source);

    private bool TryExtract(string texturemipsPath, string pngPath)
    {
        string sidecar = Path.ChangeExtension(texturemipsPath, ".texture");
        if (!File.Exists(texturemipsPath) || !File.Exists(sidecar)) return false;
        if (!ThumbnailSidecar.TryReadDims(File.ReadAllBytes(sidecar), out int w, out int h))
        {
            _log.LogWarning($"thumb {Path.GetFileName(texturemipsPath)}: sidecar has no size");
            return false;
        }

        byte[] px = File.ReadAllBytes(texturemipsPath);
        if (px.Length != (long)w * h * 4) // raw BGRA only; BC7 ships pre-decoded in Data
        {
            _log.LogWarning($"thumb {Path.GetFileName(texturemipsPath)}: " +
                $"unexpected size {px.Length} for {w}x{h}");
            return false;
        }

        BitmapSource bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
        if (ThumbnailCropper.ContentRows(px, w, h) is not (int top, int bottom) || bottom <= top)
        {
            // Uniform backdrop = the game never rendered a car into this one.
            _log.LogWarning($"thumb {Path.GetFileName(texturemipsPath)}: render shows no car");
            return false;
        }

        bmp = new CroppedBitmap(bmp, new Int32Rect(0, top, w, bottom - top));

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        Directory.CreateDirectory(Path.GetDirectoryName(pngPath)!);
        using (var fs = File.Create(pngPath)) encoder.Save(fs);
        return true;
    }
}
