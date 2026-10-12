using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace TrafficSense;

internal sealed record PluginRequest(string Operation, double[]? Values = null, int Command = 0, bool Dark = true);
internal sealed record PluginItemSample(string Id, string Name, string Label, string Value, string? Png = null);
internal sealed record PluginSample(string Name, int Api, PluginItemSample[] Items, string[] Commands, string Tooltip);

// Only instantiated by --plugin-worker. The main monitor never loads a native DLL.
internal sealed class NativePlugin : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr Factory();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int IntCall(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr PtrCall(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr IndexedPtr(IntPtr self, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void VoidCall(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void PtrVoid(IntPtr self, IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int PtrInt(IntPtr self, IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void CommandCall(IntPtr self, int index, IntPtr hwnd, IntPtr parameter);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Extended(IntPtr self, int key, IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Draw(IntPtr self, IntPtr dc, int x, int y, int width, int height, [MarshalAs(UnmanagedType.U1)] bool dark);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] [return: MarshalAs(UnmanagedType.U1)] private delegate bool DrawEx(IntPtr self, IntPtr drawer, int x, int y, int width, int height, [MarshalAs(UnmanagedType.U1)] bool dark);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] [return: MarshalAs(UnmanagedType.U1)] private delegate bool BoolCall(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate double MetricCall(IntPtr self, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr MetricTextCall(IntPtr self, int index, int main);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr ResourceCall(IntPtr self, IntPtr key, IntPtr section);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int IndexedInt(IntPtr self, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint ColorCall(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort LanguageCall(IntPtr self);
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo
    { internal ulong Up, Down; internal int Cpu, Memory, Gpu, Disk, CpuTemp, GpuTemp, DiskTemp, BoardTemp, Frequency; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void MonitorCall(IntPtr self, ref MonitorInfo info);
    private readonly IntPtr _module, _plugin, _host, _table;
    private readonly List<Delegate> _callbacks = [];
    private readonly Dictionary<string, IntPtr> _strings = [];
    private readonly IntPtr[] _metricText = Enumerable.Range(0, 13).Select(_ => Marshal.AllocHGlobal(512)).ToArray();
    private volatile double[] _values = [];
    private readonly int _api;
    private readonly string _config;
    internal static T Slot<T>(IntPtr instance, int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
    private static string Text(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero) return "";
        var chars = new List<char>();
        for (int i = 0; i < 256; i++) { char c = (char)Marshal.ReadInt16(pointer, i * 2); if (c == 0) break; if (!char.IsControl(c) || c is '\n' or '\t') chars.Add(c); }
        return new string(chars.ToArray());
    }
    private IntPtr String(string value)
    {
        lock (_strings)
        {
        if (_strings.TryGetValue(value, out var pointer)) return pointer;
        if (_strings.Count >= 256) return _strings[""];
        pointer = Marshal.StringToHGlobalUni(value); _strings.Add(value, pointer); return pointer;
        }
    }
    private double Value(int index) => index >= 0 && index < _values.Length ? _values[index] : -1;
    private IntPtr ValueText(int index)
    {
        lock (_metricText)
        {
        if (index < 0 || index >= 13) return String("");
        string text = Value(index) < 0 ? "N/A" : Value(index).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        for (int i = 0; i <= text.Length && i < 256; i++) Marshal.WriteInt16(_metricText[index], i * 2, i == text.Length ? (short)0 : (short)text[i]);
        return _metricText[index];
        }
    }
    internal NativePlugin(string path)
    {
        if (IntPtr.Size != 8 || LegacySkin.LocalPath(path) == null || Path.GetExtension(path).ToLowerInvariant() != ".dll") throw new InvalidDataException("Choose a local x64 DLL.");
        _config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrafficSense", "plugins",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(path)))));
        Directory.CreateDirectory(_config); String("");
        _module = LoadLibraryEx(path, IntPtr.Zero, 0x100 | 0x1000);
        if (_module == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        IntPtr factory = GetProcAddress(_module, "TMPluginGetInstance"); if (factory == IntPtr.Zero) throw new InvalidDataException("TMPluginGetInstance export is missing.");
        _plugin = Marshal.GetDelegateForFunctionPointer<Factory>(factory)(); if (_plugin == IntPtr.Zero) throw new InvalidDataException("Plugin returned a null instance.");
        _api = Slot<IntCall>(_plugin, 0)(_plugin); if (_api is < 1 or > 8) throw new InvalidDataException("Supported plugin API versions are 1–8.");
        // Independently constructed ITrafficMonitor v1 callback table, retained for
        // the plugin lifetime. Unknown metrics are -1, never fabricated zero.
        _callbacks.AddRange(new Delegate[] {
            new IntCall(_ => 1), new PtrCall(_ => String(typeof(App).Assembly.GetName().Version!.ToString(3))),
            new MetricCall((_, i) => Value(i)), new MetricTextCall((_, i, _) => ValueText(i)),
            new PtrVoid((_, _) => { }), new LanguageCall(_ => 0x409), new PtrCall(_ => String(_config + Path.DirectorySeparatorChar)),
            new IndexedInt((_, _) => 96), new ColorCall(_ => 0xffffff), new ResourceCall((_, _, _) => String("")),
            new PtrCall(_ => IntPtr.Zero), new PtrCall(_ => IntPtr.Zero)
        });
        _table = Marshal.AllocHGlobal(_callbacks.Count * IntPtr.Size); _host = Marshal.AllocHGlobal(IntPtr.Size);
        for (int i = 0; i < _callbacks.Count; i++) Marshal.WriteIntPtr(_table, i * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(_callbacks[i]));
        Marshal.WriteIntPtr(_host, _table);
        if (_api >= 7) Slot<PtrVoid>(_plugin, 14)(_plugin, _host);
        IntPtr config = String(_config + Path.DirectorySeparatorChar); if (_api >= 2) Slot<Extended>(_plugin, 7)(_plugin, 17, config);
    }
    internal PluginSample Execute(PluginRequest request)
    {
        _values = request.Values is { Length: <= 13 } values && values.All(double.IsFinite) ? values : [];
        int Integer(int index) => (int)Math.Clamp(Value(index), -1, int.MaxValue);
        var info = new MonitorInfo { Up = (ulong)Math.Clamp(Value(0),0,long.MaxValue), Down = (ulong)Math.Clamp(Value(1),0,long.MaxValue), Cpu=Integer(2), Memory=Integer(3), Gpu=Integer(4),
            Disk=Integer(9), CpuTemp=Integer(5), GpuTemp=Integer(6), DiskTemp=Integer(7), BoardTemp=Integer(8), Frequency=Integer(10) };
        Slot<MonitorCall>(_plugin,5)(_plugin,ref info);
        if (request.Operation == "options") Slot<PtrInt>(_plugin, 3)(_plugin, IntPtr.Zero);
        else if (request.Operation == "command")
        {
            int count = _api >= 5 ? Math.Clamp(Slot<IntCall>(_plugin, 9)(_plugin), 0, 32) : 0;
            if (request.Command < 0 || request.Command >= count) throw new InvalidDataException("Invalid plugin command.");
            Slot<CommandCall>(_plugin, 12)(_plugin, request.Command, IntPtr.Zero, IntPtr.Zero);
        }
        else if (request.Operation != "sample") throw new InvalidDataException("Unknown operation.");
        Slot<VoidCall>(_plugin, 2)(_plugin);
        var items = new List<PluginItemSample>();
        for (int i = 0; i < 32; i++)
        {
            IntPtr item = Slot<IndexedPtr>(_plugin, 1)(_plugin, i); if (item == IntPtr.Zero) break;
            string id = Text(Slot<PtrCall>(item, 1)(item)); if (id.Length == 0 || items.Any(v => v.Id == id)) throw new InvalidDataException("Invalid/duplicate plugin item ID.");
            string? png = null;
            if (Slot<BoolCall>(item, 5)(item))
            {
                int width = Math.Clamp(Slot<IntCall>(item, 6)(item), 16, 512);
                if (_api >= 3)
                { using var measure = new System.Drawing.Bitmap(1,1); using var g = System.Drawing.Graphics.FromImage(measure); nint measureDc = g.GetHdc(); try { int measured = Slot<PtrInt>(item,8)(item,measureDc); if (measured > 0) width = Math.Clamp(measured,16,512); } finally { g.ReleaseHdc(measureDc); } }
                using var bitmap = new System.Drawing.Bitmap(width, 40); using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                graphics.Clear(request.Dark ? System.Drawing.Color.Black : System.Drawing.Color.White);
                IntPtr dc = graphics.GetHdc(); try
                {
                    bool drawn = false;
                    if (_api >= 8) { using var drawer = new PluginDrawer(dc,width,40); drawn = Slot<DrawEx>(item,14)(item,drawer.Pointer,0,0,width,40,request.Dark); }
                    if (!drawn) Slot<Draw>(item, 7)(item, dc, 0, 0, width, 40, request.Dark);
                }
                finally { graphics.ReleaseHdc(dc); }
                using var stream = new MemoryStream(); bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); png = Convert.ToBase64String(stream.ToArray());
            }
            items.Add(new(id, Text(Slot<PtrCall>(item, 0)(item)), Text(Slot<PtrCall>(item, 2)(item)), Text(Slot<PtrCall>(item, 3)(item)), png));
        }
        int commandCount = _api >= 5 ? Math.Clamp(Slot<IntCall>(_plugin, 9)(_plugin), 0, 32) : 0;
        return new(Text(Slot<IndexedPtr>(_plugin, 4)(_plugin, 0)), _api, items.ToArray(),
            Enumerable.Range(0, commandCount).Select(i => Text(Slot<IndexedPtr>(_plugin, 10)(_plugin, i))).ToArray(), _api >= 2 ? Text(Slot<PtrCall>(_plugin, 6)(_plugin)) : "");
    }
    public void Dispose()
    {
        // DLL unloading can itself call arbitrary destructors. Parent enforces a
        // deadline and owns this worker process even during teardown.
        if (_module != IntPtr.Zero) FreeLibrary(_module);
        if (_host != IntPtr.Zero) Marshal.FreeHGlobal(_host); if (_table != IntPtr.Zero) Marshal.FreeHGlobal(_table);
        foreach (IntPtr value in _strings.Values) Marshal.FreeHGlobal(value);
        foreach (IntPtr value in _metricText) Marshal.FreeHGlobal(value);
        GC.KeepAlive(_callbacks);
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);
}

internal static class PluginWorker
{
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
    internal static int Run(string path)
    {
        SetErrorMode(0x1 | 0x2 | 0x8000);
        try
        {
            using var input = new StreamReader(Console.OpenStandardInput()); using var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            using var plugin = new NativePlugin(path);
            Task<string?> pending = ReadLine(input,8192); int exit = 0;
            using var timer = new System.Windows.Forms.Timer { Interval = 25 };
            timer.Tick += (_, _) =>
            {
                if (!pending.IsCompleted) return;
                try
                {
                    string? line = pending.GetAwaiter().GetResult(); if (line == null) { System.Windows.Forms.Application.ExitThread(); return; }
                    var request = JsonSerializer.Deserialize<PluginRequest>(line) ?? throw new InvalidDataException("Invalid request.");
                    string response = JsonSerializer.Serialize(plugin.Execute(request)); if (response.Length > 512 * 1024) throw new InvalidDataException("Plugin response too large.");
                    output.WriteLine(response); pending = ReadLine(input,8192);
                }
                catch { exit=2; System.Windows.Forms.Application.ExitThread(); }
            };
            timer.Start(); System.Windows.Forms.Application.Run(); return exit;
        }
        catch (Exception error)
        {
            try { using var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush=true }; output.WriteLine(JsonSerializer.Serialize(new { Error = error.Message[..Math.Min(error.Message.Length,256)] })); } catch { }
            return 2;
        }
    }
    private static async Task<string?> ReadLine(TextReader reader, int maximum)
    {
        var text = new System.Text.StringBuilder(); var one = new char[1];
        while (await reader.ReadAsync(one.AsMemory()).ConfigureAwait(false) != 0) { if (one[0] == '\n') return text.ToString(); if (text.Length >= maximum) throw new InvalidDataException("Request too large."); text.Append(one[0]); }
        return text.Length == 0 ? null : throw new EndOfStreamException();
    }
}
