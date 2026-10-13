using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TrafficSense;

internal static class NativeWindowPlacement
{
    public static void Restore(Window window, AppSettings settings, IReadOnlyList<DisplayWorkArea>? workAreas = null)
    {
        int? left = settings.ScreenLeft;
        int? top = settings.ScreenTop;
        if ((!left.HasValue || !top.HasValue) && settings.WindowLeft != -1 && settings.WindowTop != -1 &&
            settings.WindowLeft is >= int.MinValue and <= int.MaxValue &&
            settings.WindowTop is >= int.MinValue and <= int.MaxValue)
        {
            // WPF translates legacy device-independent coordinates before migration.
            window.Left = settings.WindowLeft;
            window.Top = settings.WindowTop;
            if (GetWindowRect(new WindowInteropHelper(window).Handle, out var legacy))
            {
                left = legacy.Left;
                top = legacy.Top;
            }
        }

        Place(window, left, top,workAreas);
    }

    public static void EnsureVisible(Window window, IReadOnlyList<DisplayWorkArea>? workAreas = null)
    {
        if (GetWindowRect(new WindowInteropHelper(window).Handle, out var rectangle))
            Place(window, rectangle.Left, rectangle.Top,workAreas);
    }

    public static void Capture(Window window, AppSettings settings)
    {
        settings.WindowLeft = window.Left;
        settings.WindowTop = window.Top;
        if (GetWindowRect(new WindowInteropHelper(window).Handle, out var rectangle))
        {
            settings.ScreenLeft = rectangle.Left;
            settings.ScreenTop = rectangle.Top;
        }
    }

    private static void Place(Window window, int? left, int? top,IReadOnlyList<DisplayWorkArea>? workAreas)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (!GetWindowRect(handle, out var rectangle))
            return;

        var displays = new List<DisplayWorkArea>();
        if(workAreas!=null)displays.AddRange(workAreas);
        else EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr dc, ref NativeRect bounds, IntPtr data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info) && info.Work.Right > info.Work.Left && info.Work.Bottom > info.Work.Top)
                displays.Add(new(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom, (info.Flags & 1) != 0));
            return true;
        }, IntPtr.Zero);

        if (displays.Count == 0)
            return;

        var position = WidgetPlacement.Resolve(left, top,
            rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top, displays);
        if (rectangle.Left != position.Left || rectangle.Top != position.Top)
            SetWindowPos(handle, IntPtr.Zero, position.Left, position.Top, 0, 0, 0x0001 | 0x0004 | 0x0010);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }

    private delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref NativeRect bounds, IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
