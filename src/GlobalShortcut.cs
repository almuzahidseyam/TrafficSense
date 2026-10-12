using System.Runtime.InteropServices;

namespace TrafficSense;

internal sealed class GlobalShortcut : IDisposable
{
    internal const int Id = 0x5453;
    private readonly Func<IntPtr, bool> _register;
    private readonly Action<IntPtr> _unregister;
    private IntPtr _window;
    public bool Enabled { get; private set; }
    internal GlobalShortcut(Func<IntPtr, bool>? register = null, Action<IntPtr>? unregister = null)
    {
        _register = register ?? (window => RegisterHotKey(window, Id, 0x4000 | 0x0002 | 0x0001, 0x54)); // Ctrl+Alt+T, no repeat
        _unregister = unregister ?? (window => { UnregisterHotKey(window, Id); });
    }
    public void Attach(IntPtr window) => _window = window;
    public bool SetEnabled(bool enabled)
    {
        if (enabled == Enabled) return true;
        if (enabled)
        {
            if (_window == IntPtr.Zero || !_register(_window)) return false;
            Enabled = true;
        }
        else { _unregister(_window); Enabled = false; }
        return true;
    }
    public void Dispose() => SetEnabled(false);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
