using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace AceDrive.Overlay.Views;

/// <summary>
/// Draws text as an outline instead of a fill. WPF has no text-stroke
/// primitive, so the glyphs are built as a <see cref="Geometry"/> and stroked
/// only; the design's clock (ontwerpdesign.psd) is exactly that: a white
/// outline over the backdrop.
/// <para>
/// The scale axes exist because the design stretches the clock with a text
/// transform, not a condensed cut: horizontal 0.724 and vertical 1.967 of
/// Tourney Thin. Scaling the geometry and stroking afterwards keeps the pen
/// uniform; a RenderTransform would smear the stroke with the glyph.
/// </para>
/// </summary>
public sealed class OutlinedText : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(OutlinedText),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.AffectsMeasure
            | FrameworkPropertyMetadataOptions.AffectsRender,
            OnGlyphPropertyChanged));

    public static readonly DependencyProperty FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner(
            typeof(OutlinedText),
            new FrameworkPropertyMetadata(
                SystemFonts.MessageFontFamily,
                FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsRender,
                OnGlyphPropertyChanged));

    public static readonly DependencyProperty FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner(
            typeof(OutlinedText),
            new FrameworkPropertyMetadata(
                SystemFonts.MessageFontSize,
                FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsRender,
                OnGlyphPropertyChanged));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(OutlinedText),
        new FrameworkPropertyMetadata(
            Brushes.White,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(OutlinedText),
        new FrameworkPropertyMetadata(
            1.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure
            | FrameworkPropertyMetadataOptions.AffectsRender,
            OnGlyphPropertyChanged));

    public static readonly DependencyProperty ScaleXProperty = DependencyProperty.Register(
        nameof(ScaleX), typeof(double), typeof(OutlinedText),
        new FrameworkPropertyMetadata(
            1.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure
            | FrameworkPropertyMetadataOptions.AffectsRender,
            OnGlyphPropertyChanged));

    public static readonly DependencyProperty ScaleYProperty = DependencyProperty.Register(
        nameof(ScaleY), typeof(double), typeof(OutlinedText),
        new FrameworkPropertyMetadata(
            1.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure
            | FrameworkPropertyMetadataOptions.AffectsRender,
            OnGlyphPropertyChanged));

    private Geometry? _glyphs;
    private Size _glyphSize;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public double ScaleX
    {
        get => (double)GetValue(ScaleXProperty);
        set => SetValue(ScaleXProperty, value);
    }

    public double ScaleY
    {
        get => (double)GetValue(ScaleYProperty);
        set => SetValue(ScaleYProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_glyphs is null) BuildGlyphs();
        double edge = StrokeThickness;
        return new Size(_glyphSize.Width + edge, _glyphSize.Height + edge);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_glyphs is null) BuildGlyphs();
        if (_glyphs is null) return;
        var pen = new Pen(Stroke, StrokeThickness)
        {
            LineJoin = PenLineJoin.Miter,
        };
        drawingContext.DrawGeometry(null, pen, _glyphs);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _glyphs = null;
        InvalidateMeasure();
    }

    // The geometry is cached: measure runs on every layout pass, while the
    // text only changes on a game-minute boundary.
    private static void OnGlyphPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var text = (OutlinedText)d;
        text._glyphs = null;
        text.InvalidateMeasure();
    }

    private void BuildGlyphs()
    {
        if (string.IsNullOrEmpty(Text))
        {
            _glyphs = null;
            _glyphSize = default;
            return;
        }

        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var formatted = new FormattedText(
            Text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        Geometry geometry = formatted.BuildGeometry(new Point(0, 0));
        var stretch = new Matrix();
        stretch.Scale(ScaleX, ScaleY);
        geometry.Transform = new MatrixTransform(stretch);

        // Centre the pen on the glyph edge, so the returned size covers the
        // whole stroke instead of clipping its outer half.
        Rect ink = geometry.Bounds;
        double half = StrokeThickness / 2;
        var placement = new Matrix();
        placement.Translate(half - ink.X, half - ink.Y);
        geometry.Transform = new MatrixTransform(Matrix.Multiply(stretch, placement));
        geometry.Freeze();

        _glyphs = geometry;
        _glyphSize = new Size(ink.Width, ink.Height);
    }
}
