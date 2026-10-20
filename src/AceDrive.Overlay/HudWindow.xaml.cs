using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Overlay;

/// <summary>
/// The companion window: chrome only (placement, drag, visibility, no
/// activation, save). Its text and live values come from
/// <see cref="HudViewModel"/>; the 10 Hz loop still belongs to MainViewModel.
/// </summary>
public partial class HudWindow : Window
{
    private const double DefaultWidth = 413.6;
    private const double HudHeight = 200.0;
    private const double DefaultMargin = 24.0;

    private readonly ISettingsStore _store;

    public HudWindow(
        HudViewModel viewModel, ISettingsStore store,
        double mainLeft, double mainTop, double mainWidth)
    {
        _store = store;
        DataContext = viewModel;
        InitializeComponent();

        Width = double.IsFinite(mainWidth) && mainWidth > 0.0 ? mainWidth : DefaultWidth;
        Height = HudHeight;

        var saved = _store.Load();
        if (saved is not null)
        {
            Left = saved.Left;
            Top = saved.Top;
        }
        else
        {
            // WHY anchored to the main panel: SystemParameters.WorkArea mixes
            // physical and DIP units on a scaled desktop, which once parked
            // the HUD half off-screen. The main window's Left/Top are already
            // DIPs in this window's coordinate space; default above it, and
            // fall back to below/top-left when that would leave the screen.
            Left = double.IsFinite(mainLeft) ? mainLeft : DefaultMargin;
            Top = double.IsFinite(mainTop) ? mainTop - HudHeight - DefaultMargin : DefaultMargin;
            if (Top < 0.0)
            {
                Top = double.IsFinite(mainTop) ? Math.Max(0.0, mainTop + DefaultMargin) : DefaultMargin;
            }
        }

        // WHY no-activate: dragging the HUD must not pull keyboard focus away
        // from the game. ShowActivated only covers the first Show; this covers
        // every later click on the strip.
        SourceInitialized += (_, _) =>
            Native.SetNoActivate(new WindowInteropHelper(this).Handle);
        PreviewMouseLeftButtonDown += OnDrag;
        Closed += OnClosed;
        viewModel.PropertyChanged += OnViewModelChanged;
        ApplyShown();
    }

    /// <summary>Keeps the HUD the same width as the main overlay.</summary>
    public void MatchWidth(double width)
    {
        if (double.IsFinite(width) && width > 0.0)
        {
            Width = width;
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HudViewModel.IsRevealed))
        {
            ApplyShown();
        }
    }

    // The VM owns the hand-over: hidden through the introduction, shown once
    // the birthday dialogue finishes. The window itself has no timer or rule.
    private void ApplyShown()
    {
        if (DataContext is not HudViewModel vm)
        {
            return;
        }

        if (vm.IsRevealed)
        {
            if (!IsVisible)
            {
                Show();
            }
        }
        else if (IsVisible)
        {
            Hide();
        }
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        // The ENTER button owns its click; only the surrounding chrome drags.
        if (EnterButton.IsMouseOver)
        {
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnClosed(object? sender, EventArgs e) =>
        _store.Save(new WindowBounds(Left, Top, Width, Height));
}
