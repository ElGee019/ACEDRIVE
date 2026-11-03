using System.Runtime.InteropServices;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Win32 input interop: the global hotkey, raw input (keyboard + HID) and
/// XInput. Every call is a thin declaration; the policy lives in
/// <see cref="CallInput"/>, <see cref="HidDevices"/> and
/// <see cref="XInputPads"/>. Callers must treat a failed call as "this input
/// is not available" and keep the app running (AGENTS.md boundary rule).
/// </summary>
internal static class NativeInput
{
    public const int WmInput = 0x00FF;

    // Raw input: the window observes the devices while another process owns
    // the keyboard — and, unlike a registered hotkey, without taking the press
    // away from that process.
    public const uint RidInput = 0x10000003;
    public const uint RidevInputSink = 0x00000100;
    public const uint RidiDeviceName = 0x20000007;
    public const uint RimTypeKeyboard = 1;
    public const uint RimTypeHid = 2;

    // Keyboard flags in RAWKEYBOARD: bit 0 set means the key was released.
    public const ushort RiKeyBreak = 0x0001;

    // HID: the maximum report we accept. A wheel reports well under this; a
    // larger one is refused rather than grown per message.
    public const int MaxReportLength = 4096;

    public const ushort UsagePageGenericDesktop = 0x01;
    public const ushort UsageKeyboard = 0x06;
    public const ushort UsageJoystick = 0x04;
    public const ushort UsageGamepad = 0x05;
    public const ushort UsageMultiAxis = 0x08;
    public const ushort UsagePageButton = 0x09;
    public const int HidpStatusSuccess = 0x00110000;
    public const int HidpInput = 0;

    public const int VkEscape = 0x1B;

    public const uint FileShareReadWrite = 0x00000003;
    public const uint OpenExisting = 3;

    // XInput buttons, as the pad reports them: bit index + 1 is the stored
    // code, so a rebind survives a driver update that renumbers the masks.
    public const ushort PadDpadUp = 0x0001;
    public const ushort PadDpadDown = 0x0002;
    public const ushort PadDpadLeft = 0x0004;
    public const ushort PadDpadRight = 0x0008;
    public const ushort PadStart = 0x0010;
    public const ushort PadBack = 0x0020;
    public const ushort PadLeftThumb = 0x0040;
    public const ushort PadRightThumb = 0x0080;
    public const ushort PadLeftShoulder = 0x0100;
    public const ushort PadRightShoulder = 0x0200;
    public const ushort PadA = 0x1000;
    public const ushort PadB = 0x2000;
    public const ushort PadX = 0x4000;
    public const ushort PadY = 0x8000;

    [StructLayout(LayoutKind.Sequential)]
    public struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RawKeyboard
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;

        // The C struct's Reserved[17] between the report lengths and the
        // collection counts; kept so the fields after it land correctly.
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;

        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterRawInputDevices(
        [In] RawInputDevice[] devices, uint count, uint structSize);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetRawInputData(
        IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetRawInputDeviceInfo(
        IntPtr device, uint command, IntPtr data, ref uint size);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint MapVirtualKey(uint code, uint mapType);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetKeyNameText(int lParam, [Out] char[] text, int size);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool HidD_GetPreparsedData(IntPtr device, out IntPtr preparsed);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool HidD_FreePreparsedData(IntPtr preparsed);

    [DllImport("hid.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool HidD_GetProductString(IntPtr device, char[] product, uint length);

    [DllImport("hid.dll")]
    public static extern int HidP_GetCaps(IntPtr preparsed, out HidpCaps caps);

    [DllImport("hid.dll")]
    public static extern int HidP_GetUsages(
        int reportType, ushort usagePage, ushort linkCollection,
        [Out] ushort[] usageList, ref uint usageLength,
        IntPtr preparsed, [In] byte[] report, uint reportLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateFile(
        string name, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr handle);
}
