using System.Runtime.InteropServices;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The session's opened HID devices, keyed by the raw-input device handle the
/// reports arrive on. One open per device: the path lookup, the driver open
/// and the preparsed descriptor are all paid once, not once per button press,
/// and a device that failed to open is remembered as "not available" so a
/// noisy device cannot retry the expensive path every report.
///
/// Bounded by construction: a PC has a handful of HID devices, and the map is
/// scoped to the session, not the disk.
/// </summary>
internal sealed class HidDevices : IDisposable
{
    private readonly Dictionary<IntPtr, HidDevice?> _devices = new();

    /// <summary>The device <paramref name="rawHandle"/> reports on, or null.</summary>
    public HidDevice? For(IntPtr rawHandle)
    {
        if (_devices.TryGetValue(rawHandle, out HidDevice? device))
        {
            return device;
        }

        device = Open(rawHandle);
        _devices[rawHandle] = device;
        return device;
    }

    public void Dispose()
    {
        foreach (HidDevice? device in _devices.Values)
        {
            device?.Dispose();
        }

        _devices.Clear();
        GC.SuppressFinalize(this);
    }

    private static HidDevice? Open(IntPtr rawHandle)
    {
        string? path = DevicePath(rawHandle);
        return path is null ? null : HidDevice.TryOpen(path);
    }

    // RIDI_DEVICENAME answers with a character count first, then the Unicode
    // interface path (\\?\HID#VID_...#...). The buffer is freed on every exit.
    private static string? DevicePath(IntPtr rawHandle)
    {
        uint count = 0;
        if (NativeInput.GetRawInputDeviceInfo(rawHandle, NativeInput.RidiDeviceName, IntPtr.Zero, ref count) == 0
            || count == 0)
        {
            return null;
        }

        IntPtr buffer = Marshal.AllocHGlobal((int)(count * sizeof(char)) + sizeof(char));
        try
        {
            uint size = count;
            if (NativeInput.GetRawInputDeviceInfo(rawHandle, NativeInput.RidiDeviceName, buffer, ref size) == 0)
            {
                return null;
            }

            return Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
