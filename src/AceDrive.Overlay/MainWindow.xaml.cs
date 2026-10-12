using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Overlay;

/// <summary>
/// Thin view: window chrome only (drag, hotkey hook, click-through,
/// close/save). Every behaviour lives in <see cref="MainViewModel"/> (SRP).
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();

        var saved = _vm.LoadPlacement();
        if (saved is not null)
        {
            Left = saved.Left; Top = saved.Top;
            Width = saved.Width; Height = saved.Height;
        }

        Header.MouseLeftButtonDown += (_, _) =>
        {
            if (!_vm.IsLocked) DragMove();
        };

        _vm.PropertyChanged += OnVmChanged;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
            Native.RegisterHotKey(hwnd, Native.HOTKEY_ID,
                Native.MOD_CONTROL | Native.MOD_SHIFT, Native.VK_C);
            ApplyLock();
        };
        Closed += (_, _) =>
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                Native.UnregisterHotKey(hwnd, Native.HOTKEY_ID);
            }
            catch { }
            _vm.SavePlacement(new WindowBounds(Left, Top, Width, Height));
            _vm.Dispose();
        };

        _vm.Start();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsLocked))
            ApplyLock();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == Native.HOTKEY_ID)
        {
            _vm.ToggleLockCommand.Execute(null);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ApplyLock()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        Native.SetClickThrough(hwnd, _vm.IsLocked);
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
}
