namespace AceDrive.Core;

/// <summary>
/// Crops the uniform studio backdrop off a decoded car thumbnail, keeping
/// the car band. Ported from the shell-test pipeline: background = first
/// pixel, sampled every 2nd row / 4th column; a row counts as car when the
/// per-channel deviations from the background sum beyond the tolerance.
/// The band gets asymmetric padding: <see cref="HeadroomPx"/> rows of
/// backdrop above the roofline (the detected top is the roof itself, and
/// a tight pad reads as a cut roof at thumbnail display size), <see
/// cref="MarginPx"/> below. Returns the kept rows as [Top, Bottom) or null
/// when no content was found.
/// </summary>
public static class ThumbnailCropper
{
    public const int BgTolerance = 30;   // per channel
    public const int MarginPx = 8;       // backdrop kept below the car
    public const int HeadroomPx = 20;    // backdrop kept above the roofline

    public static (int Top, int Bottom)? ContentRows(
        ReadOnlySpan<byte> bgra, int width, int height)
    {
        if (width <= 0 || height <= 0 || bgra.Length < width * height * 4) return null;
        int bgB = bgra[0], bgG = bgra[1], bgR = bgra[2];
        int stride = width * 4;
        int top = height, bottom = 0;
        for (int y = 0; y < height; y += 2)
        {
            int row = y * stride;
            for (int x = 0; x < width; x += 4)
            {
                int i = row + x * 4;
                int deviation = Math.Abs(bgra[i] - bgB)
                    + Math.Abs(bgra[i + 1] - bgG)
                    + Math.Abs(bgra[i + 2] - bgR);
                if (deviation > BgTolerance * 3)
                {
                    if (y < top) top = y;
                    bottom = y;
                    break;
                }
            }
        }

        if (bottom <= top) return null;
        return (Math.Max(0, top - HeadroomPx), Math.Min(height, bottom + MarginPx));
    }
}
