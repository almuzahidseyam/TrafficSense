using System.Runtime.InteropServices;

namespace TrafficSense;

// English PDH paths work on localized Windows. Counters are optional and never
// install drivers or require an elevated process. A missing/bad sample is null.
internal sealed class WindowsSystemMetricsReader : IDisposable
{
    private IntPtr _query, _cpu, _frequency, _gpu, _diskIdle, _diskRead, _diskWrite;
    private readonly SystemMetric _enabled;

    internal WindowsSystemMetricsReader(SystemMetric enabled)
    {
        _enabled = enabled;
        if ((enabled & ~SystemMetric.Memory) == 0 || PdhOpenQueryW(null, UIntPtr.Zero, out _query) != 0) return;
        if (enabled.HasFlag(SystemMetric.Cpu)) _cpu = Add(@"\Processor Information(_Total)\% Processor Time");
        if (enabled.HasFlag(SystemMetric.Frequency)) _frequency = Add(@"\Processor Information(_Total)\Processor Frequency");
        if (enabled.HasFlag(SystemMetric.Gpu)) _gpu = Add(@"\GPU Engine(*)\Utilization Percentage");
        if (enabled.HasFlag(SystemMetric.Disk))
        {
            _diskIdle = Add(@"\PhysicalDisk(_Total)\% Idle Time");
            _diskRead = Add(@"\PhysicalDisk(_Total)\Disk Read Bytes/sec");
            _diskWrite = Add(@"\PhysicalDisk(_Total)\Disk Write Bytes/sec");
        }
        PdhCollectQueryData(_query); // Rate counters need two observations.
    }

    private IntPtr Add(string path) => PdhAddEnglishCounterW(_query, path, UIntPtr.Zero, out var counter) == 0 ? counter : IntPtr.Zero;

    internal SystemMetrics Read()
    {
        bool collected = _query != IntPtr.Zero && PdhCollectQueryData(_query) == 0;
        var memory = new MemoryStatus { Size = (uint)Marshal.SizeOf<MemoryStatus>() };
        bool hasMemory = _enabled.HasFlag(SystemMetric.Memory) && GlobalMemoryStatusEx(ref memory);
        double? idle = collected ? ReadValue(_diskIdle) : null;
        return new(collected ? MetricMath.Percent(ReadValue(_cpu)) : null,
            hasMemory ? memory.TotalPhysical : null, hasMemory ? memory.AvailablePhysical : null,
            collected ? ReadValue(_frequency) : null, collected ? ReadGpu() : null,
            idle.HasValue ? MetricMath.Percent(100 - idle.Value) : null,
            collected ? ReadValue(_diskRead) : null, collected ? ReadValue(_diskWrite) : null);
    }

    private static double? ReadValue(IntPtr counter)
    {
        if (counter == IntPtr.Zero || PdhGetFormattedCounterValue(counter, 0x200, out _, out var value) != 0 || value.Status > 1) return null;
        return MetricMath.Valid(value.Value);
    }

    private double? ReadGpu()
    {
        if (_gpu == IntPtr.Zero) return null;
        uint size = 0, count = 0;
        const uint MoreData = 0x800007D2;
        if (PdhGetFormattedCounterArrayW(_gpu, 0x200, ref size, ref count, IntPtr.Zero) != MoreData || size == 0 || size > 8 * 1024 * 1024) return null;
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            uint capacity = size;
            if (PdhGetFormattedCounterArrayW(_gpu, 0x200, ref size, ref count, buffer) != 0) return null;
            int itemSize = Marshal.SizeOf<CounterItem>();
            if (count > 65536 || (ulong)count * (uint)itemSize > capacity) return null;
            var values = new List<(string, double)>((int)count);
            for (int i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<CounterItem>(IntPtr.Add(buffer, i * itemSize));
                if (item.Value.Status <= 1 && item.Name != IntPtr.Zero)
                    values.Add((Marshal.PtrToStringUni(item.Name) ?? "", item.Value.Value));
            }
            return MetricMath.BusiestGpuEngine(values);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    public void Dispose() { if (_query != IntPtr.Zero) { PdhCloseQuery(_query); _query = IntPtr.Zero; } }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct CounterValue { [FieldOffset(0)] public uint Status; [FieldOffset(8)] public double Value; }
    [StructLayout(LayoutKind.Sequential)]
    private struct CounterItem { public IntPtr Name; public CounterValue Value; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Size, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhOpenQueryW(string? source, UIntPtr user, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string path, UIntPtr user, out IntPtr counter);
    [DllImport("pdh.dll", ExactSpelling = true)] private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll", ExactSpelling = true)] private static extern uint PdhCloseQuery(IntPtr query);
    [DllImport("pdh.dll", ExactSpelling = true)]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out CounterValue value);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint size, ref uint count, IntPtr buffer);
}
