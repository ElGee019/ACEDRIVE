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
using AceDrive.Overlay.Views;

namespace AceDrive.Overlay;

/// <summary>
/// Thin view: visibility, drag/resize, activity reporting, hotkey hook,
/// backdrop painting, close/save. Every behaviour lives in
/// <see cref="MainViewModel"/> (SRP).
/// </summary>
public partial class MainWindow : Window
{
    private const string BackdropUri = "pack://application:,,,/Assets/backdrop.webp";

    private readonly MainViewModel _vm;
    private readonly ILog _log;
    // The call input: this window owns the HWND and its messages, the service
    // owns the hotkey, the raw devices and the assignment listener.
    private readonly IWindowInput _callInput;
    // The placement gate: a finished introduction may restore its saved window
    // size. It is the story flow's fact, read by the root at construction, so
    // the VM does not forward it.
    private readonly bool _introDone;

    // The deferred hide (ApplyShown): a blank page is painted before the window
    // goes away, so the frame the window keeps for its next Show() is the
    // chrome instead of the page being replaced.
    private bool _hidePending;
    private int _hideTicks;
    private EventHandler? _hideTick;

    public MainWindow(MainViewModel vm, ILog log, bool introDone, IWindowInput callInput)
    {
        _vm = vm;
        _log = log;
        _introDone = introDone;
        _callInput = callInput;
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
            if (_introDone && saved.Version >= WindowBounds.CurrentVersion)
            {
                Width = saved.Width;
                Height = saved.Height;
            }
        }


        // The call input must be wired BEFORE anything can create the handle:
        // SourceInitialized fires exactly once, when the HwndSource is born.
        // ApplyShown() further down (and App.Show() after this ctor) creates
        // it immediately on voice-less starts — subscribing any later means
        // the event already fired and the hotkey never gets registered at all,
        // not even to fail and log. Hidden starts are covered by EnsureHandle().
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
            // The window is opaque (AllowsTransparency=False) so it renders on
            // the GPU. The DWM rounds the corners, and the caption/thick-frame
            // styles are cleared so no white non-client strip is painted across
            // the top (resize keeps working through WM_NCHITTEST).
            Native.RoundCorners(hwnd);
            Native.SetBorderless(hwnd);
            _callInput.Attach(hwnd, () => _vm.ToggleShowCommand.Execute(null));
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
            DetachHideTick();
            _callInput.Detach();
            _vm.SavePlacement(new WindowBounds(
                Left, Top, Width, Height, WindowBounds.CurrentVersion));
            // A reset already deleted the save files and is starting a fresh
            // process: banking this session's state here would resurrect the
            // career the player just wiped.
            if (!_vm.SkipShutdownSave)
            {
                _vm.SaveClock();
                _vm.SaveCity();
                _vm.SaveHunger();
                _vm.SaveGarage();
            }
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
            CancelPendingHide();
            if (IsVisible)
            {
                return;
            }

            // The page the VM handed over while the panel was down has to be in
            // the tree before the window is presented: Show() displays the frame
            // the window kept, so laying out first means that frame already
            // carries the new page.
            PageHost.Visibility = Visibility.Visible;
            UpdateLayout();
            Show();
            return;
        }

        if (!IsVisible || _hidePending)
        {
            return;
        }

        ScheduleHide();
    }

    // WHY the hide is blanked and deferred by one painted frame: a hidden
    // window keeps the last frame it painted while it is hidden, and Show()
    // presents that frame before the page handed over since can be drawn — the
    // missing-person receipt briefly flashed the quest page it replaced.
    // Blanking the page here, then hiding once the blank frame has been
    // painted, leaves the window with nothing stale to show.
    private void ScheduleHide()
    {
        _hidePending = true;
        _hideTicks = 0;
        PageHost.Visibility = Visibility.Collapsed;
        UpdateLayout();
        _hideTick = (_, _) =>
        {
            // The first tick paints the blank frame, the second may hide.
            if (++_hideTicks < 2)
            {
                return;
            }

            FinishHide();
        };
        CompositionTarget.Rendering += _hideTick;
    }

    private void FinishHide()
    {
        DetachHideTick();
        if (!_hidePending)
        {
            return;
        }

        _hidePending = false;
        // A show cancelled it while the blank frame was being painted.
        if (_vm.IsShown)
        {
            PageHost.Visibility = Visibility.Visible;
            return;
        }

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

    private void CancelPendingHide()
    {
        if (!_hidePending)
        {
            return;
        }

        DetachHideTick();
        _hidePending = false;
        PageHost.Visibility = Visibility.Visible;
    }

    private void DetachHideTick()
    {
        if (_hideTick is not null)
        {
            CompositionTarget.Rendering -= _hideTick;
            _hideTick = null;
        }
    }

    // WHY deferred: the click's command may swap screens or hide the panel
    // first. Handing the game the foreground on the next idle turn lets that
    // work finish, then driving input resumes without a click into EVO.
    // WHY the click's origin and not its Source: a page button's Click names
    // the page host as Source, so only OriginalSource points at the button
    // (see ClickTarget). The Source test never matched, which is what silently
    // broke the hand-back for every button on every page.
    // WHY Button and not ButtonBase: the template parts that excludes are a
    // ComboBox's box/arrow and a scrollbar's thumb (ToggleButton and
    // RepeatButton). Taking the foreground mid-interaction would send the next
    // keystroke to EVO instead of the open dropdown.
    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (ClickTarget.OwnerOf(e.OriginalSource as DependencyObject) is Button)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background, new Action(Native.FocusGame));
        }
    }

    // Controls that own a click keep it; everything else is window chrome and
    // drags the panel (ClickTarget spells out which controls those are).
    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (ClickTarget.OwnerOf(e.OriginalSource as DependencyObject) is null)
        {
            DragMove();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // The call input: raw reports only. They are never marked handled, so
        // the press still reaches the game and DefWindowProc alike.
        if (_callInput.HandleWindowMessage(msg, wParam, lParam))
        {
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
        else if (msg == Native.WM_NCHITTEST && ResizeHitTest(hwnd, lParam) is { } resizeEdge)
        {
            // WHY: the caption/thick-frame styles were cleared so the DWM paints
            // no white strip across the top, but that also removed the OS resize
            // border. Reporting the bottom-right corner (and the bottom/right
            // edges) here restores the grip resize without the frame.
            handled = true;
            return new IntPtr(resizeEdge);
        }
        return IntPtr.Zero;
    }

    // Maps the mouse screen point to the client rect and reports a resize edge
    // when it lands in the grip band, or null so DefWindowProc keeps the default.
    private int? ResizeHitTest(IntPtr hwnd, IntPtr lParam)
    {
        int x = unchecked((short)(long)lParam);
        int y = unchecked((short)((long)lParam >> 16));
        if (!Native.TryScreenToClient(hwnd, x, y, out int cx, out int cy))
        {
            return null;
        }

        const int band = 8;
        int width = (int)ActualWidth;
        int height = (int)ActualHeight;
        bool bottom = cy >= height - band;
        bool right = cx >= width - band;
        if (bottom && right)
        {
            return Native.HTBOTTOMRIGHT;
        }
        if (bottom)
        {
            return Native.HTBOTTOM;
        }
        if (right)
        {
            return Native.HTRIGHT;
        }
        return null;
    }
}
