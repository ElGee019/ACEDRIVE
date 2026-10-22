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
/// Thin view: visibility, drag/resize, activity reporting, hotkey hook,
/// backdrop painting, close/save. Every behaviour lives in
/// <see cref="MainViewModel"/> (SRP).
/// </summary>
public partial class MainWindow : Window
{
    private const string BackdropUri = "pack://application:,,,/Assets/backdrop.webp";

    // Matches the alpha of the #F214161A solid fallback (0xF2 = 95 %): the
    // game stays 5 % visible through the panel.
    private const double BackdropOpacity = 0.95;

    private readonly MainViewModel _vm;
    private readonly ILog _log;

    public MainWindow(MainViewModel vm, ILog log)
    {
        _vm = vm;
        _log = log;
        DataContext = vm;
        InitializeComponent();
        TryApplyBackdrop(log);

        // Fresh position always; size only restores once past onboarding, so a
        // stale small window never clips the walk pages. The gate reads the
        // saved progress, not the page on screen: an unfinished walk resumes
        // where it left off and must still get the window the player resized
        // (its pages scroll if it is small). A pre-standard placement keeps
        // the XAML standard size: it predates the home page's proportions and
        // would otherwise bring back the scrollbar the standard size removes.
        var saved = _vm.LoadPlacement();
        if (saved is not null)
        {
            Left = saved.Left;
            Top = saved.Top;
            if (_vm.OnboardingDone && saved.Version >= WindowBounds.CurrentVersion)
            {
                Width = saved.Width;
                Height = saved.Height;
            }
        }


        // Hotkey must be wired BEFORE anything can create the handle:
        // SourceInitialized fires exactly once, when the HwndSource is born.
        // ApplyShown() further down (and App.Show() after this ctor) creates
        // it immediately on voice-less starts — subscribing any later means
        // the event already fired and RegisterHotKey never runs at all, not
        // even to fail and log. Hidden starts are covered by EnsureHandle().
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
            if (!Native.RegisterHotKey(hwnd, Native.HOTKEY_ID,
                Native.MOD_CONTROL | Native.MOD_SHIFT, Native.VK_C))
            {
                _log.LogWarning("hotkey Ctrl+Shift+C not registered; use the wake word");
            }
        };
        // WHY: App only calls Show() when the VM wants the window visible at
        // boot, and a hidden WPF window has no HwndSource — so a wake-word
        // start left the hotkey unregistered and the app unreachable by
        // keyboard until a wake word happened to show it. Forcing the handle
        // makes SourceInitialized (and the hotkey) live from startup without
        // flashing the window.
        new WindowInteropHelper(this).EnsureHandle();

        // App.Show() runs after this ctor: re-assert the VM's visibility once
        // the window is really loaded, else a hidden-at-start window would be
        // left phantom-visible (visible but IsShown=false -> idle never hides).
        Loaded += (_, _) => ApplyShown();

        ApplyShown();
        _vm.PropertyChanged += OnVmChanged;

        // Any input resets the idle-hide timer; background drag moves the window.
        PreviewMouseMove += (_, _) => _vm.RecordActivity();
        PreviewMouseDown += (_, _) => _vm.RecordActivity();
        PreviewMouseWheel += (_, _) => _vm.RecordActivity();
        PreviewKeyDown += (_, _) => _vm.RecordActivity();
        PreviewMouseLeftButtonDown += OnDragMove;
        // WHY: the panel is a normal activating window, so every click on it
        // takes the foreground from EVO and silently kills the driving input
        // until the game is clicked again. Hand the foreground back after the
        // button's command has navigated or hidden the panel. The source test
        // keeps template ToggleButton/RepeatButton parts (ComboBox arrow,
        // scrollbars) from stealing focus mid-interaction.
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
        Closed += (_, _) =>
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                Native.UnregisterHotKey(hwnd, Native.HOTKEY_ID);
            }
            catch (InvalidOperationException)
            {
                // WHY: the HWND may already be gone at Closed; WPF then
                // refuses the Handle lookup. The hotkey dies with the
                // process anyway — placement save below must still run.
            }
            _vm.SavePlacement(new WindowBounds(
                Left, Top, Width, Height, WindowBounds.CurrentVersion));
            _vm.SaveClock();
            _vm.SaveCity();
            _vm.SaveHunger();
            _vm.SaveGarage();
            _vm.Dispose();
        };

        _vm.Start();
    }

    // WHY: WPF has no WebP codec of its own — the decode rides on the OS WIC
    // one (built into Win11, possibly absent on older Win10, EVO's floor).
    // OnLoad forces the decode to throw synchronously inside this try, so a
    // machine without the codec keeps the dark XAML fallback instead of
    // crashing on the render thread later (AGENTS.md: degrade gracefully).
    private void TryApplyBackdrop(ILog log)
    {
        try
        {
            var art = new BitmapImage();
            art.BeginInit();
            art.UriSource = new Uri(BackdropUri, UriKind.Absolute);
            art.CacheOption = BitmapCacheOption.OnLoad;
            art.EndInit();
            art.Freeze();
            Backdrop.Background = new ImageBrush(art)
            {
                Stretch = Stretch.UniformToFill,
                Opacity = BackdropOpacity,
            };
            // The window keeps the XAML standard size (the home page's design
            // proportions): the art fills it and crops the small difference
            // between the 946 x 2149 backdrop and that panel aspect. Sizing
            // the panel to the art instead would re-open a gap between the
            // standard size and the page the player actually sees.
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException)
        {
            // NotSupportedException = WIC has no WebP codec on this machine;
            // IOException = resource missing from the build. Either way the
            // panel keeps its dark fallback instead of blocking window start.
            log.LogWarning($"backdrop webp not shown ({ex.GetType().Name}: {ex.Message}); using dark background");
        }
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsShown))
            ApplyShown();
    }

    private void ApplyShown()
    {
        if (_vm.IsShown)
        {
            if (!IsVisible) Show();
        }
        else if (IsVisible)
        {
            // WHY: hiding the panel while it owns the foreground (a click on a
            // form field, then the idle timer) leaves Windows with no active
            // window to hand input back to, so EVO stays deaf until clicked.
            bool ownedForeground =
                Native.IsForeground(new WindowInteropHelper(this).Handle);
            Hide();
            if (ownedForeground)
            {
                Native.FocusGame();
            }
        }
    }

    // WHY deferred: the click's command may swap screens or hide the panel
    // first. Handing the game the foreground on the next idle turn lets that
    // work finish, then driving input resumes without a click into EVO.
    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.Source is Button)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background, new Action(Native.FocusGame));
        }
    }

    // Controls that own a click keep it; everything else is window chrome.
    // ComboBoxItem matters: while the dropdown is open the ComboBox captures
    // the mouse, so a row's press tunnels through this window's preview
    // handler. DragMove() there steals the click and the row only commits on
    // Enter; letting the row keep it is what selects on a normal click.
    private static bool OwnsClick(DependencyObject source) =>
        source is ButtonBase or ComboBoxItem;

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        // The walk-up must never throw: inline content (Run/Hyperlink) is a
        // ContentElement, not a Visual, and VisualTreeHelper.GetParent on it
        // crashed the app in a live session (2026-10-13 log). Such an origin
        // simply stops the walk and counts as "not interactive" -> drag
        // allowed. 2D-only overlay, so checking Visual (not Visual3D) is enough.
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source is not null && !OwnsClick(source))
        {
            if (source is not Visual) break;
            source = VisualTreeHelper.GetParent(source);
        }

        if (source is null || !OwnsClick(source))
        {
            DragMove();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == Native.HOTKEY_ID)
        {
            _vm.ToggleShowCommand.Execute(null);
            handled = true;
        }
        else if (msg == Native.WM_ENTERSIZEMOVE)
        {
            // Idle-hide stays out of the way for the whole sizing modal loop,
            // not just while the mouse moves (leave unhandled: DefWindowProc
            // still runs the loop).
            _vm.SetResizing(true);
        }
        else if (msg == Native.WM_EXITSIZEMOVE)
        {
            _vm.SetResizing(false);
        }
        return IntPtr.Zero;
    }
}
