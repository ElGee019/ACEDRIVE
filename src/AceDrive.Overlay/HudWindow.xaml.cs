using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
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
    private bool _panelOpen;
    // A page can summon the task text while the panel is still up (pinning an
    // address); the strip then stays over the panel until the panel next opens.
    private bool _showTaskTextOverPanel;

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
        // every later click on the strip. Answering WM_MOUSEACTIVATE as well
        // closes the gap where a click still activated the strip and killed
        // EVO's input despite the extended style.
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Native.SetNoActivate(hwnd);
            HwndSource.FromHwnd(hwnd)?.AddHook(OnMessage);
        };
        PreviewMouseLeftButtonDown += OnDrag;
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
        Closed += OnClosed;
        viewModel.PropertyChanged += OnViewModelChanged;
        ApplyShown();
        ApplyRequestSize();
    }

    /// <summary>Keeps the HUD the same width as the main overlay.</summary>
    public void MatchWidth(double width)
    {
        if (double.IsFinite(width) && width > 0.0)
        {
            Width = width;
        }
    }

    /// <summary>
    /// The companion and the main panel share the same screen corner. While
    /// the panel is open the HUD normally steps aside: a topmost, no-activate
    /// strip on top of a page would cover a row and turn its click into a HUD
    /// drag (logged at Max's store), so it returns once the player drives and
    /// the panel hides again. <see cref="ShowTaskTextOverPanel"/> is the one
    /// deliberate exception, for a destination the player just pinned; it ends
    /// when that destination is entered, since there is no task text left to read.
    /// </summary>
    public void SetPanelOpen(bool open)
    {
        _panelOpen = open;
        // A fresh panel opening restores the step-aside rule; the summon only
        // bridges the panel session that was already up when it happened.
        if (open)
        {
            _showTaskTextOverPanel = false;
        }
        ApplyShown();
    }

    /// <summary>
    /// Shows the companion's task text without waiting for the panel to hide.
    /// A page calls this when it pins a destination the player should read at
    /// once; the normal step-aside resumes the next time the panel opens.
    /// </summary>
    public void ShowTaskTextOverPanel()
    {
        _showTaskTextOverPanel = true;
        ApplyShown();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HudViewModel.IsRevealed))
        {
            ApplyShown();
        }
        else if (e.PropertyName == nameof(HudViewModel.HasRequest))
        {
            ApplyRequestSize();
        }
        else if (e.PropertyName == nameof(HudViewModel.HasTaskText))
        {
            // The over-panel exception exists to surface the task text; once
            // that text is gone (the player entered the door) the companion
            // steps aside again instead of covering page rows.
            ApplyShown();
        }
    }

    // The VM owns the hand-over: hidden through the introduction, revealed once
    // the walk's closing car page is confirmed. The window itself has no timer
    // or rule; an open main panel keeps it aside until the panel closes again.
    private void ApplyShown()
    {
        if (DataContext is not HudViewModel vm)
        {
            return;
        }

        if (vm.IsRevealed && (!_panelOpen || (_showTaskTextOverPanel && vm.HasTaskText)))
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

    // A customer request takes the companion over; its page is taller than the
    // driving strip, so the frame grows to the page instead of scrolling inside
    // it, and keeps the strip's black. WHY SizeToContent: the ask's length
    // varies by quiz and language, so a fixed taller height would either clip
    // a long ask or bring the scrollbar back. MinHeight keeps a short ask (the
    // speed-challenge read-in) from shrinking the strip.
    private void ApplyRequestSize()
    {
        if (DataContext is HudViewModel { HasRequest: true })
        {
            SizeToContent = SizeToContent.Height;
            return;
        }

        // Back to the strip: the saved Height may already be the grown one.
        SizeToContent = SizeToContent.Manual;
        Height = HudHeight;
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        // Buttons own their clicks (ENTER, the request options and BACK); only
        // the surrounding chrome drags.
        if (IsOverButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    // WHY the walk: a button's label is inline content (a Run), which is a
    // ContentElement, not a Visual — VisualTreeHelper.GetParent on it throws.
    // Such an origin stops the walk and counts as "not a button" (2D-only
    // overlay, so checking Visual, not Visual3D, is enough).
    private static bool IsOverButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase)
            {
                return true;
            }

            if (source is not Visual)
            {
                return false;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    // WHY deferred: the ENTER command may open the panel or swap screens
    // first. Handing the foreground back on the next idle turn puts the game
    // on top with the click fully processed, so driving resumes at once.
    private void RestoreGameFocus() =>
        Dispatcher.BeginInvoke(
            DispatcherPriority.Background, new Action(Native.FocusGame));

    // The source test keeps template parts (a future ComboBox arrow, a
    // scrollbar) from bouncing focus to the game mid-interaction.
    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.Source is Button)
        {
            RestoreGameFocus();
        }
    }

    // WHY: answering WM_MOUSEACTIVATE directly is stronger than the extended
    // style alone — the click reaches WPF, but Windows never activates us, so
    // EVO keeps the foreground and its driving input.
    private IntPtr OnMessage(
        IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(Native.MA_NOACTIVATE);
        }

        return IntPtr.Zero;
    }

    // The request page may have grown the frame; the companion always starts
    // as the 200 px strip, so the placement file stores that height, not the
    // transient request height.
    private void OnClosed(object? sender, EventArgs e) =>
        _store.Save(new WindowBounds(Left, Top, Width, HudHeight));
}
