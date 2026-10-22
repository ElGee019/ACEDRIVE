using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace AceDrive.Overlay.Views;

/// <summary>
/// Voice acknowledgment for the menu's artwork tiles: true paints the shared
/// teal glow, false leaves the tile untouched. A tile's label is baked into
/// its bitmap, so it cannot turn its foreground teal like a text button
/// (the shared voice-ack tell); the glow traces the artwork's silhouette
/// instead. One frozen effect serves every tile: a Freezable shared across
/// elements must be frozen, and a frozen effect costs nothing to re-attach.
/// </summary>
public sealed class BoolToVoiceGlowConverter : IValueConverter
{
    private static readonly DropShadowEffect VoiceGlow = CreateGlow();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? VoiceGlow : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static DropShadowEffect CreateGlow()
    {
        var glow = new DropShadowEffect
        {
            Color = Color.FromRgb(0x97, 0xEA, 0xD2),
            ShadowDepth = 0,
            BlurRadius = 18,
            Opacity = 1.0,
        };
        glow.Freeze();
        return glow;
    }
}
