using System.Runtime.InteropServices;
using System.Threading;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The one owner of the call input: the pressed chord, wheel button or pad
/// button that brings the panel back from the driver's seat, plus the setup
/// page's one-shot capture that assigns it.
///
/// The stream is observed, never claimed. That is the reason there is no
/// RegisterHotKey here: a global hotkey swallows its key system-wide, so a
/// wheel button or a letter bound to the call would stop reaching the game
/// (and every other program). Raw input reports the same press to this window
/// with the key still delivered to whoever owns the focus, so the call works
/// from the driver's seat without taking the input away from EVO. The trade is
/// that a bound key does both things at once, which is what a "call" the
/// player chose on purpose should do.
///
/// Each device family is read the only way it can be:
/// — a keyboard chord arrives as a raw-input keyboard report, with the held
///   modifiers read from the OS at that moment;
/// — a HID button (wheel, DirectInput pad) arrives as a raw-input HID report,
///   which needs no polling and no foreground window;
/// — an XInput pad has no notification API at all, so it is polled at 25 ms,
///   and only while a call is being captured or an XInput button is armed.
///   That is the one loop this service owns; nothing else here runs on a timer.
///
/// The armed binding is kept here rather than in a store: the preferences page
/// writes it through <see cref="Arm"/>, and the menu reads back
/// <see cref="Binding"/> for its "you can also press …" line.
/// </summary>
public sealed class CallInput : IWindowInput, ICallBinder, ICallArmer, IDisposable
{
    private const int PadPollMilliseconds = 25;
    private const int MaxUsages = 64;

    private readonly ILog _log;
    private readonly IClock _clock;
    private readonly HidDevices _hid = new();
    private readonly XInputPads _pads = new();
    private readonly ushort[] _usages = new ushort[MaxUsages];
    // One pinned native buffer for the session: raw reports are read into it
    // and copied out per report, so no allocation happens per message.
    private readonly byte[] _reports = new byte[NativeInput.MaxReportLength];
    private readonly IntPtr _rawBuffer;

    private Action? _call;
    private Action<CallBinding?>? _capture;
    private SynchronizationContext? _ui;
    private CallBinding _binding = CallBinding.Default;
    private Timer? _padTimer;
    private ushort _heldPadButtons;
    private bool _padLogged;
    private bool _attached;

    public CallInput(ILog log, IClock clock)
    {
        _log = log;
        _clock = clock;
        _rawBuffer = Marshal.AllocHGlobal(NativeInput.MaxReportLength);
        if (_rawBuffer == IntPtr.Zero)
        {
            _log.LogWarning("call input: no buffer for raw reports; controller buttons are off");
        }
    }

    /// <summary>The input that currently calls the panel.</summary>
    public CallBinding Binding => _binding;

    /// <summary>
    /// Binds the listener to the window and to the work a call does. Called
    /// once the HWND exists; arming before that is remembered and applied here.
    /// </summary>
    public void Attach(IntPtr window, Action callAction)
    {
        _call = callAction;
        _ui = SynchronizationContext.Current;
        _attached = true;
        RegisterRawInput(window);
        ApplyArming();
    }

    /// <summary>Stops every watcher; the window is gone or closing.</summary>
    public void Detach()
    {
        _attached = false;
        _call = null;
        StopPadPolling();
    }

    /// <summary>
    /// One window message: the raw-input reports the call is read from. It is
    /// never marked handled, because a foreground report must still reach
    /// DefWindowProc or the system stops delivering them — and because the
    /// press belongs to the game as much as to this window.
    /// </summary>
    public bool HandleWindowMessage(int message, IntPtr wParam, IntPtr lParam)
    {
        if (message == NativeInput.WmInput)
        {
            ReadRawInput(lParam);
        }

        return false;
    }

    /// <summary>
    /// Listens for the next input and answers once. The armed call is ignored
    /// for the duration (the capture is checked first), so pressing it while
    /// assigning cannot toggle the panel mid-capture; <see cref="Arm"/> (from
    /// the preferences write) or the fallback in <see cref="FinishCapture"/>
    /// puts a call back afterwards.
    /// </summary>
    public void BeginCapture(Action<CallBinding?> captured)
    {
        _capture = captured;
        ApplyArming();
    }

    /// <summary>Stops listening without a result (Escape, or a second tap).</summary>
    public void CancelCapture()
    {
        if (_capture is not null)
        {
            FinishCapture(null);
        }
    }

    /// <summary>Arms <paramref name="binding"/>; null keeps the shipped chord.</summary>
    public void Arm(CallBinding? binding)
    {
        _binding = CallBinding.OrDefault(binding);
        ApplyArming();
    }

    public void Dispose()
    {
        Detach();
        StopPadPolling();
        _hid.Dispose();
        if (_rawBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_rawBuffer);
        }

        GC.SuppressFinalize(this);
    }

    private void FinishCapture(CallBinding? binding)
    {
        Action<CallBinding?>? captured = _capture;
        if (captured is null)
        {
            return;
        }

        _capture = null;
        captured(binding);
        // The page's preferences write arms the new binding inside that
        // callback; a cancel (or a page that cannot arm) falls back to the
        // binding that was armed before the capture started.
        ApplyArming();
    }

    private void ApplyArming()
    {
        if (!_attached)
        {
            return;
        }

        // A pad still has to be watched while capturing even when the armed
        // binding is a chord: the player may be assigning the pad right now.
        bool capturing = _capture is not null;
        bool watchPads = capturing || _binding.Kind == CallBindingKind.PadButton;
        if (watchPads)
        {
            StartPadPolling();
        }
        else
        {
            StopPadPolling();
        }
    }

    private void RegisterRawInput(IntPtr window)
    {
        if (_rawBuffer == IntPtr.Zero)
        {
            return;
        }

        NativeInput.RawInputDevice[] devices =
        [
            Sink(NativeInput.UsageKeyboard, window),
            Sink(NativeInput.UsageJoystick, window),
            Sink(NativeInput.UsageGamepad, window),
            Sink(NativeInput.UsageMultiAxis, window),
        ];
        if (!NativeInput.RegisterRawInputDevices(
            devices, (uint)devices.Length, (uint)Marshal.SizeOf<NativeInput.RawInputDevice>()))
        {
            _log.LogWarning("call input: raw devices not registered; wheel and pad buttons are off");
        }
    }

    private static NativeInput.RawInputDevice Sink(ushort usage, IntPtr window) => new()
    {
        UsagePage = NativeInput.UsagePageGenericDesktop,
        Usage = usage,
        Flags = NativeInput.RidevInputSink,
        Target = window,
    };

    private void ReadRawInput(IntPtr rawInput)
    {
        uint size = NativeInput.MaxReportLength;
        uint headerSize = (uint)Marshal.SizeOf<NativeInput.RawInputHeader>();
        uint read = NativeInput.GetRawInputData(
            rawInput, NativeInput.RidInput, _rawBuffer, ref size, headerSize);
        if (read == uint.MaxValue || size == 0)
        {
            return;
        }

        NativeInput.RawInputHeader header =
            Marshal.PtrToStructure<NativeInput.RawInputHeader>(_rawBuffer);
        int offset = Marshal.SizeOf<NativeInput.RawInputHeader>();
        if (header.Type == NativeInput.RimTypeKeyboard)
        {
            ReadKeyboard(offset);
        }
        else if (header.Type == NativeInput.RimTypeHid)
        {
            ReadHidButton(header.Device, offset, (int)size);
        }
    }

    private void ReadKeyboard(int offset)
    {
        NativeInput.RawKeyboard key = Marshal.PtrToStructure<NativeInput.RawKeyboard>(_rawBuffer + offset);
        // Raw input reports the release too, and only the press is a call.
        if ((key.Flags & NativeInput.RiKeyBreak) != 0 || IsModifierKey(key.VKey))
        {
            return;
        }

        if (_capture is not null)
        {
            if (key.VKey == NativeInput.VkEscape)
            {
                FinishCapture(null);
                return;
            }

            CallModifiers held = HeldModifiers();
            FinishCapture(new CallBinding(
                CallBindingKind.Keyboard, key.VKey, held,
                InputNames.Chord(key.VKey, held), null));
            return;
        }

        // The armed chord, matched with the modifiers held right now: the same
        // sound the capture made when the player pressed it.
        if (_binding.Kind == CallBindingKind.Keyboard
            && _binding.Code == key.VKey
            && _binding.Modifiers == HeldModifiers())
        {
            _log.LogInfo($"call from {_binding.Label}");
            _call?.Invoke();
        }
    }

    private void ReadHidButton(IntPtr device, int offset, int received)
    {
        if (_capture is null && _binding.Kind != CallBindingKind.HidButton)
        {
            return;
        }

        int reportSize = Marshal.ReadInt32(_rawBuffer, offset);
        int reportCount = Marshal.ReadInt32(_rawBuffer, offset + sizeof(int));
        int dataOffset = offset + (2 * sizeof(int));
        if (reportSize <= 0 || reportCount <= 0
            || reportSize > _reports.Length || dataOffset + reportSize > received)
        {
            return;
        }

        HidDevice? hid = _hid.For(device);
        if (hid is null)
        {
            return;
        }

        Marshal.Copy(_rawBuffer + dataOffset, _reports, 0, reportSize);
        int pressed = hid.ReadNewPress(_reports, reportSize, _usages, _clock.UtcNow);
        if (pressed == 0)
        {
            return;
        }

        if (_capture is not null)
        {
            FinishCapture(new CallBinding(
                CallBindingKind.HidButton, pressed, CallModifiers.None,
                InputNames.HidButton(pressed), hid.Identity));
            return;
        }

        if (_binding.Kind == CallBindingKind.HidButton
            && _binding.Code == pressed
            && string.Equals(_binding.Device, hid.Identity, StringComparison.OrdinalIgnoreCase))
        {
            _log.LogInfo($"call from {hid.Product} button {pressed}");
            _call?.Invoke();
        }
    }

    private void StartPadPolling()
    {
        if (_padTimer is not null)
        {
            return;
        }

        if (!_pads.IsAvailable)
        {
            if (!_padLogged)
            {
                _padLogged = true;
                _log.LogWarning("call input: no XInput runtime; Xbox pads cannot be assigned");
            }

            return;
        }

        // Seed with what is held now: a button already down when the listener
        // arms is not a press of its own.
        _heldPadButtons = _pads.ReadButtons();
        _padTimer = new Timer(_ => PollPads(), null, PadPollMilliseconds, PadPollMilliseconds);
    }

    private void StopPadPolling()
    {
        _padTimer?.Dispose();
        _padTimer = null;
    }

    private void PollPads()
    {
        ushort buttons = _pads.ReadButtons();
        ushort pressed = (ushort)(buttons & ~_heldPadButtons);
        _heldPadButtons = buttons;
        if (pressed == 0)
        {
            return;
        }

        // The capture answer and the panel call both touch the UI thread's
        // state, so the press is handed back there instead of running here.
        _ui?.Post(_ => OnPadPressed(pressed), null);
    }

    private void OnPadPressed(ushort pressed)
    {
        int code = InputNames.PadCode(pressed);
        if (code == 0)
        {
            return;
        }

        var binding = new CallBinding(
            CallBindingKind.PadButton, code, CallModifiers.None,
            InputNames.PadButton(code), null);
        if (_capture is not null)
        {
            FinishCapture(binding);
            return;
        }

        if (_binding.Kind == CallBindingKind.PadButton && _binding.Code == code)
        {
            _log.LogInfo($"call from pad button {binding.Label}");
            _call?.Invoke();
        }
    }

    // The keys whose own press is not a binding: a chord is the modifier plus
    // a key, and a bare Ctrl is not something a player means to press.
    private static bool IsModifierKey(int virtualKey) => virtualKey is
        0x10 or 0x11 or 0x12 or 0x5B or 0x5C
        or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;

    // Read from the OS at the moment the chord's key goes down: tracking the
    // modifier keys from the raw stream alone misses one that was already held
    // when the listener started.
    private static CallModifiers HeldModifiers()
    {
        CallModifiers modifiers = CallModifiers.None;
        if (IsHeld(0x11)) modifiers |= CallModifiers.Control;
        if (IsHeld(0x10)) modifiers |= CallModifiers.Shift;
        if (IsHeld(0x12)) modifiers |= CallModifiers.Alt;
        if (IsHeld(0x5B) || IsHeld(0x5C)) modifiers |= CallModifiers.Windows;
        return modifiers;
    }

    private static bool IsHeld(int virtualKey) =>
        (NativeInput.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
}
