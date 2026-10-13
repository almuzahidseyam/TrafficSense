using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using TrafficSense;

internal static class PluginSoakProbe
{
    private sealed record Snapshot(double Seconds,double CpuSeconds,long WorkingSet,long PrivateBytes,int Handles,uint Gdi,uint User);
    private sealed class Session(CommunityPluginProbe.Case test,string config,PluginClient client,Process worker,PluginSample first):IDisposable
    {
        internal readonly CommunityPluginProbe.Case Test=test;internal readonly string Config=config;
        internal readonly PluginClient Client=client;internal readonly Process Worker=worker;
        internal readonly string[] Ids=first.Items.Select(i=>i.Id).ToArray();internal readonly string[] InitialValues=first.Items.Select(i=>i.Value).ToArray();
        internal readonly List<Snapshot> Resources=[];internal int Samples,Dialogs,Commands;internal bool ValuesChanged;
        public void Dispose(){Client.Dispose();Worker.Dispose();}
    }
    internal static int Run(string manifest,int seconds,string reportPath)
    {
        if(seconds is <30 or >86400)throw new ArgumentOutOfRangeException(nameof(seconds));
        var cases=CommunityPluginProbe.ReadCases(manifest);var sessions=new List<Session>();var errors=new List<string>();var results=new List<object>();var watch=Stopwatch.StartNew();
        string root=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!,"soak-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string? old=Environment.GetEnvironmentVariable("TRAFFICSENSE_TEST_DIALOGS");Environment.SetEnvironmentVariable("TRAFFICSENSE_TEST_DIALOGS","1");
        using var host=Process.GetCurrentProcess();Snapshot? hostWarm=null,hostEnd=null;double duration=0;int restartCycles=0;
        try
        {
            foreach(var test in cases)
            {
                string config=Path.Combine(root,test.Name);Directory.CreateDirectory(config);if(test.Name=="TextReader")CommunityInteractionProbe.SeedText(test.Dll,config);
                var client=CommunityInteractionProbe.CreateClient(test.Dll,config);
                try{var first=client.Request(new("sample",[12345])).GetAwaiter().GetResult();sessions.Add(new(test,config,client,CommunityInteractionProbe.OpenNativeWorker(config),first));}
                catch{client.Dispose();throw;}
            }
            Console.WriteLine($"SOAK: {sessions.Count} pinned native workers; 10 s warmup, then {seconds} s sampling/drawing, theme changes and repeated modal operations.");
            double warmEnd=watch.Elapsed.TotalSeconds+10;
            while(watch.Elapsed.TotalSeconds<warmEnd){foreach(var session in sessions)Poll(session,true);Thread.Sleep(700);}
            foreach(var session in sessions)session.Resources.Add(Capture(session.Worker,watch));hostWarm=Capture(host,watch);
            double start=watch.Elapsed.TotalSeconds,nextCapture=start+30,nextDialog=start+90,nextProgress=start+60;
            while(watch.Elapsed.TotalSeconds-start<seconds)
            {
                var tick=Stopwatch.StartNew();bool dark=(int)(watch.Elapsed.TotalSeconds-start)%2==0;
                foreach(var session in sessions)Poll(session,dark);
                if(watch.Elapsed.TotalSeconds>=nextDialog)
                {
                    foreach(var session in sessions)
                    {
                        var observations=new List<PluginDialogDriver.Observation>();
                        var request=new PluginRequest(session.Test.Name=="TextReader"?"command":"options",[12345],session.Test.Name=="TextReader"?5:0,dark);
                        CommunityInteractionProbe.Dialog(session.Client,session.Config,request,"Cancel",observations);session.Dialogs++;
                        if(session.Test.Name=="TextReader")foreach(int command in new[]{1,0,4,4}){session.Client.Request(new("command",[12345],command,dark)).GetAwaiter().GetResult();session.Commands++;}
                    }
                    nextDialog+=90;
                }
                if(watch.Elapsed.TotalSeconds>=nextCapture){foreach(var session in sessions)session.Resources.Add(Capture(session.Worker,watch));nextCapture+=30;}
                if(watch.Elapsed.TotalSeconds>=nextProgress){Console.WriteLine($"SOAK: {watch.Elapsed.TotalSeconds-start:F0}/{seconds} s; {sessions.Sum(s=>s.Samples)} successful samples; {sessions.Sum(s=>s.Dialogs)} modal operations.");nextProgress+=60;}
                int remaining=(int)Math.Max(0,1000-tick.Elapsed.TotalMilliseconds);if(remaining>0)Thread.Sleep(remaining);
            }
            duration=watch.Elapsed.TotalSeconds-start;
            foreach(var session in sessions)
            {
                var end=Capture(session.Worker,watch);session.Resources.Add(end);var before=session.Resources[0];
                Check(end.Handles-before.Handles<=32 && (long)end.Gdi-before.Gdi<=8 && (long)end.User-before.User<=8,"Worker native handles grew beyond existing community budgets.");
                Check(end.PrivateBytes-before.PrivateBytes<=64L*1024*1024,"Worker private memory grew beyond 64 MiB after warmup.");
                Check(session.Samples>=seconds*.8,"Too few successful steady-state samples.");
                if(session.Test.Name=="DateTime")Check(session.ValuesChanged,"Clock did not advance during soak.");
                results.Add(new{session.Test.Name,session.Test.Tag,session.Test.DllSHA256,session.Samples,session.Dialogs,session.Commands,session.ValuesChanged,
                    Seconds=end.Seconds-before.Seconds,CpuPercentOfOneCore=(end.CpuSeconds-before.CpuSeconds)/(end.Seconds-before.Seconds)*100,
                    PrivateGrowthMiB=(end.PrivateBytes-before.PrivateBytes)/(1024d*1024),HandleGrowth=end.Handles-before.Handles,GdiGrowth=(long)end.Gdi-before.Gdi,UserGrowth=(long)end.User-before.User,
                    PeakWorkingSetMiB=session.Resources.Max(s=>s.WorkingSet)/(1024d*1024),Resources=session.Resources,Passed=true});
                session.Client.Dispose();Check(session.Worker.WaitForExit(2000),"Normal Stop left a native worker alive.");
            }
            foreach(var session in sessions)session.Dispose();
            // Repeated launches use the same owned configuration but fresh jobs.
            foreach(var session in sessions)for(int cycle=0;cycle<3;cycle++)
            {
                using var client=CommunityInteractionProbe.CreateClient(session.Test.Dll,session.Config);client.Request(new("sample",[12345])).GetAwaiter().GetResult();
                using var worker=CommunityInteractionProbe.OpenNativeWorker(session.Config);client.Dispose();Check(worker.WaitForExit(2000),"Reload cycle left a native worker alive.");restartCycles++;
            }
            hostEnd=Capture(host,watch);Check(hostEnd.Handles-hostWarm.Handles<=32,"Host handles grew beyond 32 after worker cleanup/reloads.");
            Check(hostEnd.PrivateBytes-hostWarm.PrivateBytes<=64L*1024*1024,"Host private memory grew beyond 64 MiB after warmup.");
        }
        catch(Exception e){errors.Add(e.ToString());Console.Error.WriteLine(e);}
        finally{foreach(var session in sessions)session.Dispose();Environment.SetEnvironmentVariable("TRAFFICSENSE_TEST_DIALOGS",old);}
        File.WriteAllText(reportPath,JsonSerializer.Serialize(new{Format="TrafficSense.PluginSoak",Version=1,AppVersion=typeof(App).Assembly.GetName().Version!.ToString(3),TestedAt=DateTimeOffset.UtcNow,
            RequestedSeconds=seconds,MeasuredSeconds=duration,WallClockSeconds=watch.Elapsed.TotalSeconds,Results=results,HostWarm=hostWarm,HostAfterCleanup=hostEnd,RestartCycles=restartCycles,Errors=errors,Passed=errors.Count==0,
            Scope="Finite same-machine private-desktop diagnostic run; four pinned community binaries, isolated configuration and owned text, normal GC, no forced collections. CPU includes the test dialog-driver timer. Per-worker budgets: +32 kernel handles, +8 GDI, +8 USER, +64 MiB private memory. Not an hours/days soak or hardware/driver certification."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"{(errors.Count==0?"PASS":"FAIL")}: plugin soak {duration:F1} s; {restartCycles} reload cycles; {reportPath}");return errors.Count==0?0:1;
    }
    private static void Poll(Session session,bool dark)
    {
        var sample=session.Client.Request(new("sample",[12345],Dark:dark)).GetAwaiter().GetResult();
        Check(sample.Items.Select(i=>i.Id).SequenceEqual(session.Ids),"Item identity changed during soak.");
        foreach(var item in sample.Items.Where(i=>i.Png!=null))PluginImage.Decode(item.Png!);
        session.ValuesChanged|=!sample.Items.Select(i=>i.Value).SequenceEqual(session.InitialValues);session.Samples++;
    }
    private static Snapshot Capture(Process process,Stopwatch watch){process.Refresh();Check(!process.HasExited,"Native worker exited during soak.");return new(watch.Elapsed.TotalSeconds,process.TotalProcessorTime.TotalSeconds,process.WorkingSet64,process.PrivateMemorySize64,process.HandleCount,GetGuiResources(process.Handle,0),GetGuiResources(process.Handle,1));}
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    [DllImport("user32.dll")]private static extern uint GetGuiResources(nint process,uint flags);
}
