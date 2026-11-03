using System.Windows;
using System.Windows.Controls;

namespace AceDrive.Overlay.Views;

/// <summary>
/// The design's game-clock header, shared by the menu, the calendar page and
/// the jobs page. The caller supplies the two display strings; the view owns
/// only the PSD geometry, so every page renders the same clock without
/// duplicating it.
/// </summary>
public partial class GameClockView : UserControl
{
    /// <summary>The design's full band: the menu's tile line and the
    /// calendar's strip line both start at y = 942 on the 1080 x 2280 canvas.</summary>
    public const double DesignBandHeight = 942;

    /// <summary>The band trimmed to the weekday line's own line box (815 on
    /// the design canvas, rounded up for a hair of air): a page whose body
    /// follows the clock directly puts its content one blank line under the
    /// weekday instead of under the band's empty tail.</summary>
    public const double InkBandHeight = 820;

    public static readonly DependencyProperty ClockTextProperty = DependencyProperty.Register(
        nameof(ClockText), typeof(string), typeof(GameClockView),
        new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty DayWeekTextProperty = DependencyProperty.Register(
        nameof(DayWeekText), typeof(string), typeof(GameClockView),
        new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty BandHeightProperty = DependencyProperty.Register(
        nameof(BandHeight), typeof(double), typeof(GameClockView),
        new FrameworkPropertyMetadata(DesignBandHeight));

    public GameClockView() => InitializeComponent();

    /// <summary>24-hour game time, drawn as the outlined clock face.</summary>
    public string ClockText
    {
        get => (string)GetValue(ClockTextProperty);
        set => SetValue(ClockTextProperty, value);
    }

    /// <summary>Abbreviated weekday and display week below the clock.</summary>
    public string DayWeekText
    {
        get => (string)GetValue(DayWeekTextProperty);
        set => SetValue(DayWeekTextProperty, value);
    }

    /// <summary>Height of the design band, in the 1080-wide grid's units.
    /// <see cref="DesignBandHeight"/> keeps the menu's and the calendar's
    /// geometry; <see cref="InkBandHeight"/> trims the empty tail below the
    /// weekday line for a page whose body starts right under the clock.</summary>
    public double BandHeight
    {
        get => (double)GetValue(BandHeightProperty);
        set => SetValue(BandHeightProperty, value);
    }
}
