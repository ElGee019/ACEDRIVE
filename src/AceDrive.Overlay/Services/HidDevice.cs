using System.Text;

namespace AceDrive.Overlay.Services;

/// <summary>
/// One opened HID device (a wheel, a DirectInput pad, a shifter): its product
/// name, its stable vendor/product identity and a button read that turns one
/// raw report into the button usages it holds. Opened lazily the first time
/// the device reports, kept for the session — a driver handle is expensive to
/// acquire and the device set does not change under us in normal play.
///
/// WHY the identity is vendor/product and not the device path: Windows builds
/// the path from the USB port, so replugging a wheel into another port would
/// otherwise drop a binding the player assigned to one of its buttons.
/// </summary>
internal sealed class HidDevice : IDisposable
{
    // A fresh press of the same button is ignored for this long. WHY: a device
    // that streams axis reports alongside its buttons can show an empty button
    // set between two full ones while a button is still held, which would
    // otherwise look like a press on every frame.
    private static readonly TimeSpan RepeatGuard = TimeSpan.FromMilliseconds(500);

    private readonly IntPtr _handle;
    private readonly IntPtr _preparsed;
    // Reused per report: HidP_GetUsages wants the whole input report, and the
    // raw-input buffer can be shorter (a device may report fewer bytes).
    private readonly byte[] _report;
    // The buttons the previous report held, so a held button is not a press.
    private readonly ushort[] _holding;
    private int _holdingCount;
    private DateTime _lastPress = DateTime.MinValue;

    private HidDevice(
        IntPtr handle, IntPtr preparsed, byte[] report, string identity, string product)
    {
        _handle = handle;
        _preparsed = preparsed;
        _report = report;
        _holding = new ushort[64];
        Identity = identity;
        Product = product;
    }

    /// <summary>Vendor/product identity, e.g. "VID_046D&amp;PID_C24F".</summary>
    public string Identity { get; }

    /// <summary>The device's product string, e.g. "G29 Driving Force Racing Wheel".</summary>
    public string Product { get; }

    /// <summary>
    /// Opens <paramref name="path"/>, or null when the device cannot be
    /// opened or parsed (a wheel that is still starting up): the caller then
    /// ignores that device instead of failing the whole listener.
    /// </summary>
    public static HidDevice? TryOpen(string path)
    {
        IntPtr handle = NativeInput.CreateFile(
            path, 0, NativeInput.FileShareReadWrite, IntPtr.Zero,
            NativeInput.OpenExisting, 0, IntPtr.Zero);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            return null;
        }

        if (!NativeInput.HidD_GetPreparsedData(handle, out IntPtr preparsed))
        {
            _ = NativeInput.CloseHandle(handle);
            return null;
        }

        if (NativeInput.HidP_GetCaps(preparsed, out NativeInput.HidpCaps caps)
            != NativeInput.HidpStatusSuccess)
        {
            _ = NativeInput.HidD_FreePreparsedData(preparsed);
            _ = NativeInput.CloseHandle(handle);
            return null;
        }

        // The report length the device declares, so HidP_GetUsages always sees
        // a complete report even when raw input handed us a shorter one.
        var report = new byte[Math.Max((int)caps.InputReportByteLength, 1)];
        return new HidDevice(handle, preparsed, report, IdentityOf(path), ProductOf(handle));
    }

    /// <summary>
    /// Reads the buttons held in one raw report into <paramref name="usages"/>.
    /// Returns the count, or -1 when the report does not match this device's
    /// preparsed data. The caller diffs the result against the previous read,
    /// so a held button is not reported as a fresh press.
    /// </summary>
    public int ReadButtons(byte[] report, int length, ushort[] usages)
    {
        if (length <= 0 || length > _report.Length)
        {
            return -1;
        }

        Array.Clear(_report);
        Array.Copy(report, _report, length);
        uint count = (uint)usages.Length;
        int status = NativeInput.HidP_GetUsages(
            NativeInput.HidpInput, NativeInput.UsagePageButton, 0,
            usages, ref count, _preparsed, _report, (uint)_report.Length);
        return status == NativeInput.HidpStatusSuccess ? (int)count : -1;
    }

    /// <summary>
    /// Reads one raw report and answers the button that just went down (0 when
    /// none). Held buttons are not presses: the set the previous report held is
    /// remembered here, and a repeat inside <see cref="RepeatGuard"/> is
    /// dropped so a device that interleaves axis reports cannot fire twice for
    /// one press. <paramref name="now"/> comes from the caller's clock.
    /// </summary>
    public int ReadNewPress(byte[] report, int length, ushort[] usages, DateTime now)
    {
        int count = ReadButtons(report, length, usages);
        if (count < 0)
        {
            return 0;
        }

        int pressed = 0;
        for (int i = 0; i < count && pressed == 0; i++)
        {
            if (Array.IndexOf(_holding, usages[i], 0, _holdingCount) < 0)
            {
                pressed = usages[i];
            }
        }

        _holdingCount = count;
        Array.Copy(usages, _holding, count);
        if (pressed == 0 || now - _lastPress < RepeatGuard)
        {
            return 0;
        }

        _lastPress = now;
        return pressed;
    }

    public void Dispose()
    {
        _ = NativeInput.HidD_FreePreparsedData(_preparsed);
        _ = NativeInput.CloseHandle(_handle);
        GC.SuppressFinalize(this);
    }

    // VID_xxxx&PID_xxxx is stable across ports; a device that does not expose
    // one (an emulated shifter) keeps its path as the identity.
    private static string IdentityOf(string path)
    {
        int vid = path.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
        int pid = path.IndexOf("PID_", StringComparison.OrdinalIgnoreCase);
        return vid >= 0 && pid > vid
            ? path.Substring(vid, pid - vid + 8).ToUpperInvariant()
            : path;
    }

    private static string ProductOf(IntPtr handle)
    {
        var product = new char[128];
        return NativeInput.HidD_GetProductString(handle, product, (uint)(product.Length * sizeof(char)))
            ? new string(product).TrimEnd('\0')
            : string.Empty;
    }
}
