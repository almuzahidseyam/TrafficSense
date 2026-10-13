using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using TrafficSense;
using Forms=System.Windows.Forms;

internal static class VpnEnvironmentProbe
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint Counters(ref Guid id,out ulong rx,out ulong tx);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint Facts(ref Guid id,out uint index,out uint hardware);
    [DllImport("iphlpapi.dll",ExactSpelling=true)] private static extern uint GetBestInterface(uint address,out uint index);
    private sealed class Lane(string role,string? manual)
    {
        internal readonly string Role=role; internal readonly string? Manual=manual;
        internal readonly NetworkAdapterSelection Selector=new(); internal readonly NetworkSpeedCalculator Calculator=new();
        internal NetworkAdapterSnapshot? First,Last; internal long Received,Sent; internal string? Selected; internal int Fresh;
    }
    internal static int Run(string fixture,string reportPath)
    {
        nint module=NativeLibrary.Load(Path.GetFullPath(fixture));
        try
        {
            var read=Marshal.GetDelegateForFunctionPointer<Counters>(NativeLibrary.GetExport(module,"ReadNicCounters"));
            var facts=Marshal.GetDelegateForFunctionPointer<Facts>(NativeLibrary.GetExport(module,"ReadNicFacts"));
            var watch=Stopwatch.StartNew(); var initial=NetworkSnapshotReader.Read(()=>watch.Elapsed).Where(a=>a.CanMonitor).ToArray();
            var hardwareIds=new HashSet<string>(); var indexById=new Dictionary<string,uint>();
            foreach(var adapter in initial)
            { Guid id=Guid.Parse(adapter.Id); if(facts(ref id,out uint index,out uint hardware)==0){indexById[adapter.Id]=index; if(hardware!=0)hardwareIds.Add(adapter.Id);} }
            var tunnel=initial.FirstOrDefault(a=>a.Description.Contains("WARP",StringComparison.OrdinalIgnoreCase));
            var physical=initial.FirstOrDefault(a=>hardwareIds.Contains(a.Id));
            var routes=new List<object>();
            foreach(string destination in new[]{"1.1.1.1","8.8.8.8","9.9.9.9","93.184.216.34","140.82.112.3"})
            { uint error=GetBestInterface(BitConverter.ToUInt32(IPAddress.Parse(destination).GetAddressBytes()),out uint index);
                routes.Add(new{Destination=destination,Status=error,SelectedRole=error!=0?"Unavailable":tunnel!=null&&indexById.GetValueOrDefault(tunnel.Id)==index?"Observed WARP tunnel":physical!=null&&indexById.GetValueOrDefault(physical.Id)==index?"Physical interface":"Other interface"}); }
            var cases=new List<object>();
            foreach(int interval in new[]{250,500,1000,2000})
            {
                var lanes=new List<Lane>{new("Auto",null)}; if(tunnel!=null)lanes.Add(new("Manual WARP",tunnel.Id)); if(physical!=null)lanes.Add(new("Manual physical",physical.Id));
                var rows=new List<object>();
                for(int i=0;i<=10;i++)
                {
                    var before=new Dictionary<string,(ulong Rx,ulong Tx)>();
                    foreach(var adapter in initial)
                    { Guid id=Guid.Parse(adapter.Id); if(read(ref id,out ulong rx,out ulong tx)==0)before[adapter.Id]=(rx,tx); }
                    var adapters=NetworkSnapshotReader.Read(()=>watch.Elapsed); var time=watch.Elapsed;
                    foreach(var adapter in adapters.Where(a=>a.Snapshot!=null && before.ContainsKey(a.Id)))
                    {
                        Guid id=Guid.Parse(adapter.Id); var sample=adapter.Snapshot!.Value; var low=before[adapter.Id];
                        if(read(ref id,out ulong rx,out ulong tx)!=0 || (ulong)sample.BytesReceived<low.Rx || (ulong)sample.BytesReceived>rx || (ulong)sample.BytesSent<low.Tx || (ulong)sample.BytesSent>tx)
                            throw new InvalidDataException("Native counter bracket mismatch or live interface changed.");
                    }
                    var readings=new List<object>();
                    foreach(var lane in lanes)
                    {
                        var selected=lane.Selector.Select(adapters,lane.Manual);
                        if(selected?.Snapshot is not {} sample)throw new InvalidDataException("Required live interface disappeared.");
                        if(!before.ContainsKey(selected.Id))throw new InvalidDataException("Independent native counter bracket unavailable for selected interface.");
                        lane.Selected??=selected.Id; if(lane.Selected!=selected.Id)throw new InvalidDataException("Route changed during a steady-state case; rerun after it settles.");
                        var speed=lane.Calculator.Update([sample],time); lane.First??=sample; lane.Last=sample;
                        lane.Received+=lane.Calculator.LastTraffic.Received; lane.Sent+=lane.Calculator.LastTraffic.Sent;
                        if(i>0 && lane.Calculator.Quality!=SampleQuality.Fresh)throw new InvalidDataException("Live sampling gap; case is inconclusive.");
                        if(i>0)lane.Fresh++;
                        readings.Add(new{lane.Role,Received=sample.BytesReceived,Sent=sample.BytesSent,Elapsed=time.TotalSeconds,speed.DownloadBytesPerSecond,speed.UploadBytesPerSecond,Quality=lane.Calculator.Quality.ToString()});
                    }
                    rows.Add(new{Sample=i,Readings=readings}); if(i<10)Thread.Sleep(interval);
                }
                foreach(var lane in lanes)
                    if(lane.Received!=lane.Last!.Value.BytesReceived-lane.First!.Value.BytesReceived || lane.Sent!=lane.Last!.Value.BytesSent-lane.First!.Value.BytesSent)throw new InvalidDataException("Exact observed byte conservation failed.");
                var auto=lanes[0]; bool autoIsTunnel=tunnel!=null&&auto.Selected==tunnel.Id;
                var matchingManual=lanes.FirstOrDefault(l=>l.Manual==auto.Selected);
                bool autoMatchesOne=matchingManual!=null && auto.Received==matchingManual.Received && auto.Sent==matchingManual.Sent;
                if(matchingManual!=null && !autoMatchesOne)throw new InvalidDataException("Auto differs from the same manually selected interface.");
                var tunnelLane=lanes.FirstOrDefault(l=>l.Role=="Manual WARP"); var physicalLane=lanes.FirstOrDefault(l=>l.Role=="Manual physical");
                bool sumWouldDiffer=tunnelLane!=null && physicalLane!=null &&
                    (auto.Received!=tunnelLane.Received+physicalLane.Received || auto.Sent!=tunnelLane.Sent+physicalLane.Sent);
                var totals=lanes.Select(lane=>new{lane.Role,lane.Fresh,lane.Received,lane.Sent,ByteConservationPass=true}).ToArray();
                cases.Add(new{IntervalMilliseconds=interval,AutoIsObservedTunnel=autoIsTunnel,CounterBracketsPass=true,AutoMatchesOneManualInterface=autoMatchesOne,AddingTunnelAndPhysicalWouldDiffer=sumWouldDiffer,Totals=totals,Samples=rows});
                Console.WriteLine($"PASS: live {interval} ms; {lanes.Count} simultaneous lanes; Auto on observed WARP={autoIsTunnel}; exact per-interface byte conservation.");
            }
            var displays=Forms.Screen.AllScreens.Select(s=>new{s.Bounds.Left,s.Bounds.Top,s.Bounds.Width,s.Bounds.Height,s.Primary}).ToArray();
            File.WriteAllText(reportPath,JsonSerializer.Serialize(new{Format="TrafficSense.VpnEnvironment",Version=1,TestedAt=DateTimeOffset.UtcNow,Windows=Environment.OSVersion.VersionString,
                ObservedWarpPresent=tunnel!=null,PhysicalInterfacePresent=physical!=null,ConnectedInterfaces=initial.Length,RouteLookups=routes,Cases=cases,Displays=displays,
                Scope="Passive steady-state WARP/physical-interface comparison; independent native brackets. No adapter, route, VPN, display or shell settings changed. No payload-throughput or reconnect certification.",
                Untested=new[]{"Physical VPN reconnect", "Physical second monitor and mixed DPI", "Controlled LAN payload throughput", "Other VPN products"}},new JsonSerializerOptions{WriteIndented=true}));
            return 0;
        }
        finally{NativeLibrary.Free(module);}
    }
}
