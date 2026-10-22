using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AceDrive.Overlay.Views;

/// <summary>
/// The receipt's XP world-standings rank line. A positive
/// <see cref="PulseSignal"/> runs Unity's ReceiptValuePop animation: the line
/// scales from 1x to 1.5x and back, twice, over two real seconds. Every new
/// positive signal restarts the pulse (Unity StopCoroutine + StartCoroutine),
/// while 0 stops and resets it (ResetScales), so a no-change board result
/// cancels an in-flight pulse. Presentation lives here, not in the
/// ViewModel; the signal is the only thing the page supplies.
/// </summary>
public sealed class StandingsLine : TextBlock
{
    private const double MaxScale = 1.5;
    private const double HalfPulseSeconds = 0.5;
    private const double PulseSeconds = 2.0;

    public static readonly DependencyProperty PulseSignalProperty = DependencyProperty.Register(
        nameof(PulseSignal), typeof(int), typeof(StandingsLine),
        new PropertyMetadata(0, OnPulseSignalChanged));

    public StandingsLine()
    {
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = new ScaleTransform(1.0, 1.0);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Positive restarts the pulse; 0 stops and resets it.</summary>
    public int PulseSignal
    {
        get => (int)GetValue(PulseSignalProperty);
        set => SetValue(PulseSignalProperty, value);
    }

    private static void OnPulseSignalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not StandingsLine line) return;

        int signal = (int)e.NewValue;
        if (signal <= 0)
        {
            line.StopPulse();
        }
        else if (line.IsLoaded)
        {
            line.StartPulse();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (PulseSignal > 0) StartPulse();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopPulse();

    private void StartPulse()
    {
        if (RenderTransform is not ScaleTransform scale) return;
        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty, PulseAnimation(),
            HandoffBehavior.SnapshotAndReplace);
        scale.BeginAnimation(
            ScaleTransform.ScaleYProperty, PulseAnimation(),
            HandoffBehavior.SnapshotAndReplace);
    }

    private void StopPulse()
    {
        if (RenderTransform is not ScaleTransform scale) return;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = 1.0;
        scale.ScaleY = 1.0;
    }

    // Two full up/down cycles, linear like ReceiptValuePop's PingPong wave:
    // 1x at 0 s and 1 s and 2 s, 1.5x at 0.5 s and 1.5 s.
    private static DoubleAnimationUsingKeyFrames PulseAnimation() =>
        new()
        {
            Duration = new Duration(TimeSpan.FromSeconds(PulseSeconds)),
            KeyFrames =
            {
                new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                new LinearDoubleKeyFrame(
                    MaxScale, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(HalfPulseSeconds))),
                new LinearDoubleKeyFrame(
                    1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2 * HalfPulseSeconds))),
                new LinearDoubleKeyFrame(
                    MaxScale, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3 * HalfPulseSeconds))),
                new LinearDoubleKeyFrame(
                    1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(PulseSeconds))),
            },
        };
}
