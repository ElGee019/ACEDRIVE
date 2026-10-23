using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AceDrive.Core;
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
    private const string BlueBackdropUri = "pack://application:,,,/Assets/bluebg.webp";

    // The blue plate inside the backdrop art's transparent export margin (see
    // TryLoadBackdrop), in the brush's relative units. A re-export at another
    // canvas size only trims a sliver of art instead of exposing the margin.
    private static readonly Rect BackdropPlate = new(
        30.0 / 1080.0, 30.0 / 2280.0, 1020.0 / 1080.0, 2220.0 / 2280.0);

    // The XAML fallback fill, kept so the request toggle can restore it when
    // the WebP backdrop is unavailable.
    private static readonly Brush DarkBackdrop =
        new SolidColorBrush(Color.FromArgb(0xF2, 0x14, 0x16, 0x1A));

    private readonly ISettingsStore _store;
    private readonly ILog _log;
    private readonly Brush? _blueBackdrop;
    private bool _panelOpen;
    // A page can summon the task text while the panel is still up (pinning an
    // address); the strip then stays over the panel until the panel next opens.
    private bool _showTaskTextOverPanel;

    public HudWindow(
        HudViewModel viewModel, ISettingsStore store, ILog log,
        double mainLeft, double mainTop, double mainWidth)
    {
        _store = store;
        _log = log;
        DataContext = viewModel;
        InitializeComponent();
        _blueBackdrop = TryLoadBackdrop();

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
        ApplyRequest();
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
    /// deliberate exception, for a destination the player just pinned.
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
            ApplyRequest();
        }
    }

    // The VM owns the hand-over: hidden through the introduction, revealed once
    // the birthday dialogue finishes. The window itself has no timer or rule;
    // an open main panel keeps it aside until the panel closes again.
    private void ApplyShown()
    {
        if (DataContext is not HudViewModel vm)
        {
            return;
        }

        if (vm.IsRevealed && (!_panelOpen || _showTaskTextOverPanel))
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

    // The customer request takes the companion over: the frame turns blue while
    // the ask is up and reverts when the host clears it. The frame keeps the
    // driving strip's size — the page scrolls inside it instead of growing over
    // the game. A machine without the WebP codec keeps the dark fill and still
    // shows the ask.
    private void ApplyRequest()
    {
        bool request = DataContext is HudViewModel { HasRequest: true };
        Backdrop.Background = request ? _blueBackdrop ?? DarkBackdrop : DarkBackdrop;
    }

    // WHY: WPF has no WebP codec of its own — the decode rides on the OS WIC
    // one. OnLoad forces the decode to throw synchronously inside this try, so
    // a codec-less machine keeps the dark XAML fill instead of crashing on the
    // render thread later (AGENTS.md: degrade gracefully).
    private ImageBrush? TryLoadBackdrop()
    {
        try
        {
            var art = new BitmapImage();
            art.BeginInit();
            art.UriSource = new Uri(BlueBackdropUri, UriKind.Absolute);
            art.CacheOption = BitmapCacheOption.OnLoad;
            art.EndInit();
            art.Freeze();
            return new ImageBrush(art)
            {
                Stretch = Stretch.UniformToFill,
                // WHY the Viewbox: the art is exported on a 1080 x 2280 canvas
                // whose blue plate is the 1020 x 2220 content inside a 30 px
                // transparent margin. Mapping the whole canvas leaves that
                // margin transparent around the frame edge — at the companion's
                // width the white ring then shows right at the window border.
                // Mapping just the plate lets UniformToFill reach the edge.
                Viewbox = BackdropPlate,
            };
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException)
        {
            // NotSupportedException = WIC has no WebP codec on this machine;
            // IOException = resource missing from the build. Either way the
            // request keeps its dark fill instead of blocking window start.
            _log.LogWarning($"blue request backdrop not shown ({ex.GetType().Name}: {ex.Message}); using dark fill");
            return null;
        }
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

    private void OnClosed(object? sender, EventArgs e) =>
        _store.Save(new WindowBounds(Left, Top, Width, Height));
}
