using System.Runtime.InteropServices;

namespace TrafficSense;

internal readonly record struct DockBounds(int Left, int Top, int Right, int Bottom);
internal interface IAppBarShell
{
    bool Register(IntPtr window, uint callback);
    DockBounds Query(IntPtr window, bool top, DockBounds bounds);
    DockBounds Set(IntPtr window, bool top, DockBounds bounds);
    void Move(IntPtr window, DockBounds bounds);
    void Notify(IntPtr window, uint message);
    void Unregister(IntPtr window);
    void FullScreen(IntPtr window, bool active);
    void Arrange(IntPtr window, bool hiding);
}

internal sealed class AppBarShell : IAppBarShell
{
    private static AppBarData Data(IntPtr window) => new() { Size = (uint)Marshal.SizeOf<AppBarData>(), Window = window };
    public bool Register(IntPtr window, uint callback)
    { var data = Data(window); data.Callback = callback; return SHAppBarMessage(0, ref data) != UIntPtr.Zero; }
    public DockBounds Query(IntPtr window, bool top, DockBounds bounds) => Position(window, top, bounds, 2);
    public DockBounds Set(IntPtr window, bool top, DockBounds bounds) => Position(window, top, bounds, 3);
    private static DockBounds Position(IntPtr window, bool top, DockBounds bounds, uint message)
    {
        var data = Data(window); data.Edge = top ? 1u : 3u;
        data.Bounds = new() { Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom };
        SHAppBarMessage(message, ref data);
        return new(data.Bounds.Left, data.Bounds.Top, data.Bounds.Right, data.Bounds.Bottom);
    }
    public void Move(IntPtr window, DockBounds bounds) => SetWindowPos(window, new IntPtr(-1), bounds.Left, bounds.Top,
        bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0x10);
    public void Notify(IntPtr window, uint message) { var data = Data(window); SHAppBarMessage(message, ref data); }
    public void Unregister(IntPtr window) { var data = Data(window); SHAppBarMessage(1, ref data); }
    public void FullScreen(IntPtr window, bool active) => SetWindowPos(window, active ? new IntPtr(1) : new IntPtr(-1), 0, 0, 0, 0, 0x13);
    public void Arrange(IntPtr window, bool hiding) => ShowWindow(window, hiding ? 0 : 4);

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData { public uint Size; public IntPtr Window; public uint Callback, Edge; public Rect Bounds; public IntPtr Parameter; }
    [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(IntPtr window, int command);
}
