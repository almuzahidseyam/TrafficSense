using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TrafficSense;

// Kernel ownership survives a monitor crash: closing the last job handle kills
// only processes explicitly assigned to this worker's job, including its children.
internal sealed class WorkerJob : IDisposable
{
    private readonly SafeFileHandle _handle;
    internal WorkerJob(System.Diagnostics.Process process)
    {
        _handle = CreateJobObject(IntPtr.Zero, null);
        if (_handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var limits = new Limits { Basic = new BasicLimits { Flags = 0x2000 } }; // KILL_ON_JOB_CLOSE
        if (!SetInformationJobObject(_handle,9,ref limits,Marshal.SizeOf<Limits>()) || !AssignProcessToJobObject(_handle,process.Handle))
        { int error = Marshal.GetLastWin32Error(); _handle.Dispose(); throw new Win32Exception(error); }
    }
    public void Dispose() => _handle.Dispose();
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
    { internal long ProcessTime,JobTime; internal uint Flags; internal nuint MinWorkingSet,MaxWorkingSet; internal uint ActiveProcesses; internal nuint Affinity; internal uint Priority,Scheduling; }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters
    { internal ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] private struct Limits
    { internal BasicLimits Basic; internal IoCounters Io; internal nuint ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory; }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern SafeFileHandle CreateJobObject(IntPtr attributes,string? name);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool SetInformationJobObject(SafeFileHandle job,int kind,ref Limits limits,int size);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool AssignProcessToJobObject(SafeFileHandle job,IntPtr process);
}
