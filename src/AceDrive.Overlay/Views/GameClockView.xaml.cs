using System.Windows;
using System.Windows.Controls;

namespace AceDrive.Overlay.Views;

/// <summary>
/// The design's game-clock header, shared by the menu and the calendar page.
/// The caller supplies the two display strings; the view owns only the PSD
/// geometry, so both pages render the same clock without duplicating it.
/// </summary>
public partial class GameClockView : UserControl
{
    public static readonly DependencyProperty ClockTextProperty = DependencyProperty.Register(
        nameof(ClockText), typeof(string), typeof(GameClockView),
        new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty DayWeekTextProperty = DependencyProperty.Register(
        nameof(DayWeekText), typeof(string), typeof(GameClockView),
        new FrameworkPropertyMetadata(string.Empty));

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
}
