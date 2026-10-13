using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TrafficSense;

internal enum PluginWorkerAvailability { Available, Unavailable, Exited }
internal sealed record PluginWorkerHealth(PluginWorkerAvailability Availability, double? CpuPercent,
    long? WorkingSetBytes, long? PrivateBytes, TimeSpan Uptime, TimeSpan? LastResponseAge);

// Interval CPU time as a share of all active logical processors, like a
// machine-wide process percentage. Missing/reset/delayed observations rebase.
internal sealed class PluginCpuSampler(int processors)
{
    private readonly int _processors=processors>0?processors:throw new ArgumentOutOfRangeException(nameof(processors));
    private (TimeSpan Time,TimeSpan Cpu)? _previous;
    internal void Reset()=>_previous=null;
    internal double? Update(TimeSpan time,TimeSpan cpu)
    {
        if(time<TimeSpan.Zero || cpu<TimeSpan.Zero){Reset();return null;}
        var previous=_previous;_previous=(time,cpu);
        if(previous is not {} before || time<=before.Time || cpu<before.Cpu || time-before.Time>TimeSpan.FromSeconds(5))return null;
        return Math.Clamp((cpu.Ticks-before.Cpu.Ticks)/(double)(time.Ticks-before.Time.Ticks)/_processors*100,0,100);
    }
}

// A separate Process observer avoids racing the request/resource-limit reader's
// Refresh calls. Reads are run off the UI thread and never terminate a plugin.
internal sealed class PluginHealthMonitor:IDisposable
{
    private readonly object _gate=new();
    private readonly long _started=Stopwatch.GetTimestamp();
    private readonly PluginCpuSampler _cpu=new(ProcessorCount());
    private Process? _observer;
    private bool _disposed;
    internal PluginHealthMonitor(int worker)
    {
        try{_observer=Process.GetProcessById(worker);_= _observer.Handle;}
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){_observer?.Dispose();_observer=null;}
    }
    internal PluginWorkerHealth Read(long lastResponse)
    {
        lock(_gate)
        {
            TimeSpan uptime=Stopwatch.GetElapsedTime(_started);
            TimeSpan? age=lastResponse==0?null:Stopwatch.GetElapsedTime(lastResponse);
            if(age<TimeSpan.Zero)age=TimeSpan.Zero;
            PluginWorkerHealth Missing(PluginWorkerAvailability availability){_cpu.Reset();return new(availability,null,null,null,uptime,age);}
            if(_disposed)return Missing(PluginWorkerAvailability.Exited);
            if(_observer==null)return Missing(PluginWorkerAvailability.Unavailable);
            try
            {
                _observer.Refresh();
                if(_observer.HasExited)return Missing(PluginWorkerAvailability.Exited);
                TimeSpan cpu=_observer.TotalProcessorTime;
                long resident=_observer.WorkingSet64,allocated=_observer.PrivateMemorySize64;
                uptime=Stopwatch.GetElapsedTime(_started);
                if(resident<0 || allocated<0)return Missing(PluginWorkerAvailability.Unavailable);
                return new(PluginWorkerAvailability.Available,_cpu.Update(uptime,cpu),resident,allocated,uptime,age);
            }
            catch(Exception e) when(e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException){return Missing(PluginWorkerAvailability.Unavailable);}
        }
    }
    public void Dispose(){lock(_gate){if(_disposed)return;_disposed=true;_observer?.Dispose();_observer=null;_cpu.Reset();}}
    private static int ProcessorCount(){uint count=GetActiveProcessorCount(0xffff);return count>0?(int)count:Math.Max(1,Environment.ProcessorCount);}
    [DllImport("kernel32.dll")]private static extern uint GetActiveProcessorCount(ushort group);
}
