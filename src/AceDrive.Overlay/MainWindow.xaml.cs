using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    public MainWindow(MainViewModel vm, ILog log)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
        TryApplyBackdrop(log);

        // Fresh position always; size only restores once past onboarding, so a
        // stale small window never clips the first-run screens. The gate reads
        // the saved progress, not the page on screen: the introduction replays
        // on every call, so a returning player opens on the brand page and must
        // still get the window they resized (its pages scroll if it is small).
        var saved = _vm.LoadPlacement();
        if (saved is not null)
        {
            Left = saved.Left;
            Top = saved.Top;
            if (_vm.OnboardingDone)
            {
                Width = saved.Width;
                Height = saved.Height;
            }
        }


        // App.Show() runs after this ctor: re-assert the VM's visibility once
        // the window is really loaded, else a hidden-at-start window would be
        // left phantom-visible (visible but IsShown=false -> idle never hides).
        Loaded += (_, _) => ApplyShown();

        ApplyShown();
        _vm.PropertyChanged += OnVmChanged;

        // Freeze the first-rendered size: every later screen keeps the first
        // screen's size instead of resizing to its own text.
        ContentRendered += OnFirstRendered;

        // Any input resets the idle-hide timer; background drag moves the window.
        PreviewMouseMove += (_, _) => _vm.RecordActivity();
        PreviewMouseDown += (_, _) => _vm.RecordActivity();
        PreviewMouseWheel += (_, _) => _vm.RecordActivity();
        PreviewKeyDown += (_, _) => _vm.RecordActivity();
        PreviewMouseLeftButtonDown += OnDragMove;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
            Native.RegisterHotKey(hwnd, Native.HOTKEY_ID,
                Native.MOD_CONTROL | Native.MOD_SHIFT, Native.VK_C);
        };
        // WHY: App only calls Show() when the VM wants the window visible at
        // boot, and a hidden WPF window has no HwndSource — so a wake-word
        // start left Ctrl+Shift+C unregistered and the app unreachable by
        // keyboard until a wake word happened to show it. Forcing the handle
        // makes SourceInitialized (and the hotkey) live from startup without
        // flashing the window.
        new WindowInteropHelper(this).EnsureHandle();
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
            _vm.SavePlacement(new WindowBounds(Left, Top, Width, Height));
            _vm.SaveClock();
            _vm.SaveCity();
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
            // Base resolution follows the backdrop's native aspect: the design
            // width (360) stays, the height is derived from the decoded image
            // (960x1024 -> 384) so the panel shows the whole artwork without
            // UniformToFill cropping. Saved placement below still overrides it.
            SizeToContent = SizeToContent.Manual;
            Height = Width * art.PixelHeight / art.PixelWidth;
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

    private void OnFirstRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnFirstRendered;
        if (SizeToContent != SizeToContent.Manual)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            SizeToContent = SizeToContent.Manual;
            Width = width;
            Height = height;
        }
    }

    private void ApplyShown()
    {
        if (_vm.IsShown)
        {
            if (!IsVisible) Show();
        }
        else if (IsVisible)
        {
            Hide();
        }
    }

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        // Buttons keep their clicks; anywhere else drags the borderless window.
        // The walk-up must never throw: inline content (Run/Hyperlink) is a
        // ContentElement, not a Visual, and VisualTreeHelper.GetParent on it
        // crashed the app in a live session (2026-10-13 log). Such an origin
        // simply stops the walk and counts as "not a button" -> drag allowed.
        // 2D-only overlay, so checking Visual (not Visual3D) is enough.
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source is not null && source is not ButtonBase)
        {
            if (source is not Visual) break;
            source = VisualTreeHelper.GetParent(source);
        }

        if (source is not ButtonBase)
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
