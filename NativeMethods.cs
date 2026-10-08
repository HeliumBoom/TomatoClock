using System.Runtime.InteropServices;

namespace TomatoClock;

internal static class NativeMethods
{
    public const int WmNcHitTest = 0x84, WmNcDoubleClick = 0xA3, WmNcRightButtonUp = 0xA5, WmPowerBroadcast = 0x218;
    public const int HtCaption = 2;
    public const int WsExTransparent = 0x20, WsExLayered = 0x80000;
    public const int WsExToolWindow = 0x80, WsExAppWindow = 0x40000;
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetLayeredWindowAttributes(IntPtr window, uint colorKey, byte alpha, uint flags);
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseTracking
    {
        public uint Size, Flags;
        public IntPtr Window;
        public uint HoverTime;
    }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TrackMouseEvent(ref MouseTracking tracking);
    public static void TrackNonclientHover(IntPtr window)
    {
        var tracking = new MouseTracking
        {
            Size = (uint)Marshal.SizeOf<MouseTracking>(), Flags = 0x13,
            Window = window, HoverTime = 450
        };
        TrackMouseEvent(ref tracking);
    }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr window);
    public static Point MessagePoint(IntPtr value) => new(
        unchecked((short)(value.ToInt64() & 0xffff)), unchecked((short)((value.ToInt64() >> 16) & 0xffff)));
}
