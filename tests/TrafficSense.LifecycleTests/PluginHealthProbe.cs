using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using TrafficSense;

internal static class PluginHealthProbe
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    internal static int Run(string dll)
    {
        using(var client=new PluginClient(Environment.GetEnvironmentVariable("TRAFFICSENSE_PACKAGE_EXE")??Environment.ProcessPath!,dll))
        {
            var first=client.ReadHealth();Check(first.Availability==PluginWorkerAvailability.Available&&first.CpuPercent==null&&first.LastResponseAge==null,"First health observation fabricated CPU/response data.");
            client.Request(new("sample",[12345])).GetAwaiter().GetResult();
            var baseline=client.ReadHealth();Check(baseline.WorkingSetBytes>0&&baseline.PrivateBytes>0&&baseline.LastResponseAge>=TimeSpan.Zero,"Real worker RAM/response data missing.");
            client.Request(new("command",[12345],4)).GetAwaiter().GetResult();
            var busy=client.ReadHealth();Check(busy.CpuPercent>0&&busy.CpuPercent<=100,"Real CPU work did not register a bounded interval percentage.");
            var process=(Process)typeof(PluginClient).GetField("_process",Private)!.GetValue(client)!;
            var input=process.StandardInput.BaseStream;var output=process.StandardOutput.BaseStream;
            client.Dispose();var stopped=client.ReadHealth();Check(stopped.Availability==PluginWorkerAvailability.Exited&&stopped.CpuPercent==null&&stopped.WorkingSetBytes==null,"Disposed worker retained metrics.");
            Check(!input.CanWrite&&!output.CanRead,"Worker shutdown retained redirected pipe handles.");
        }
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;
        PluginBridgeWindow? bridge=null;string root=Path.Combine(Path.GetTempPath(),"TrafficSense.Health-"+Guid.NewGuid().ToString("N"));
        string? oldModal=Environment.GetEnvironmentVariable("TRAFFICSENSE_FIXTURE_MODAL");
        app.Startup+=async(_,_)=>
        {
            var gate=new ManualResetEventSlim(true);
            try
            {
                int reads=0,active=0,peak=0;bool fail=false,delay=false;var language=new UiLanguage("en");
                bridge=new(language,()=>[12345],_=>{},()=>false,path=>new PluginClient(Environment.ProcessPath!,path,root),client=>
                {
                    int running=Interlocked.Increment(ref active);Interlocked.Increment(ref reads);peak=Math.Max(peak,running);
                    try{if(delay&&!gate.Wait(4000))throw new TimeoutException("Owned health gate timed out.");if(fail)throw new IOException("Owned health failure.");return client.ReadHealth();}
                    finally{Interlocked.Decrement(ref active);}
                }){Opacity=0,ShowActivated=false,ShowInTaskbar=false,Height=250};
                bridge.Show();
                T Field<T>(string name)=>(T)typeof(PluginBridgeWindow).GetField(name,Private)!.GetValue(bridge)!;
                Task Refresh()=>(Task)typeof(PluginBridgeWindow).GetMethod("RefreshHealth",Private)!.Invoke(bridge,null)!;
                void Click(string name)=>Field<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var state=Field<TextBlock>("_healthState");var usage=Field<TextBlock>("_healthUsage");
                var history=Field<PluginHealthHistory>("_healthHistory");var graph=Field<PluginHealthGraph>("_healthGraph");
                Check(state.Text.Contains(language[TextKey.PluginHealthIdle])&&usage.Text.Contains(language[TextKey.NotAvailable]),"Idle health is incorrect.");
                Check(bridge.Content is ScrollViewer,"Bridge cannot scroll in a short window.");
                Check(await bridge.Open(dll),"Health UI fixture did not open.");
                Check(state.Text.Contains(language[TextKey.PluginHealthResponding])&&usage.Text.Contains("MiB"),"Live worker health missing.");
                Check(history.Samples.Count>0&&history.Samples.Last().WorkingSetBytes>0,"Real worker readings did not reach history.");
                var keyEvent=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(graph)!,0,Key.Home){RoutedEvent=Keyboard.KeyDownEvent};
                graph.RaiseEvent(keyEvent);
                Check(keyEvent.Handled&&graph.HoveredPoint!=null&&Field<TextBlock>("_historyDetails").Text==graph.KeyboardDetails,"Routed keyboard inspection did not reach visible details.");
                var selectedTime=graph.HoveredPoint?.Timestamp;await Refresh();
                Check(graph.HoveredPoint?.Timestamp==selectedTime,"Live worker refresh moved keyboard selection.");
                Check(AutomationProperties.GetName(usage)==usage.Text&&AutomationProperties.GetLiveSetting(state)==AutomationLiveSetting.Polite,"Health accessibility lost its values/state.");
                language.Select("bn");Check(state.Text.Contains(language[TextKey.PluginHealthResponding])&&usage.Text.Contains(language[TextKey.Memory])&&AutomationProperties.GetName(graph).Contains(language[TextKey.PluginHistory]),"Live health/history language did not update.");language.Select("en");
                fail=true;await Refresh();Check(state.Text.Contains(language[TextKey.PluginHealthUnavailable])&&usage.Text.Contains(language[TextKey.NotAvailable])&&Field<Button>("_stop").IsEnabled,"Failed health read stopped worker or showed stale metrics.");
                Check(history.Samples.Last().CpuPercent==null&&history.Samples.Last().WorkingSetBytes==null,"Unavailable health became measured graph zero.");
                fail=false;await Refresh();Check(state.Text.Contains(language[TextKey.PluginHealthResponding]),"Health did not recover after an unavailable read.");
                Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_MODAL","2");
                // Reload owns the environment inherited by its new worker.
                Check(await bridge.Open(dll),"Modal health worker did not open.");
                Click("_options");Check(state.Text.Contains(language[TextKey.PluginHealthSettings]),"Settings busy state missing.");
                int before=reads;await Wait(()=>reads>before&&!Field<bool>("_healthBusy"));
                Check(Field<bool>("_busy")&&Field<PluginWorkerHealth>("_latestHealth").Uptime>TimeSpan.FromMilliseconds(500),"Health timer stopped during modal settings.");
                Check(history.Samples.Count>=2,"History stopped during modal settings.");
                Click("_stop");await Wait(()=>!Field<bool>("_busy"));
                Check(state.Text.Contains(language[TextKey.PluginHealthStopped])&&!usage.Text.Contains("MiB")&&!Field<DispatcherTimer>("_healthTimer").IsEnabled,"Stop retained numbers or health timer.");
                Check(history.Samples.Count==0,"Stop retained worker history.");
                Check(graph.HoveredPoint==null&&Field<TextBlock>("_historyDetails").Text==language[TextKey.PluginHistoryKeyboard],"Stop retained keyboard detail text.");
                Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_MODAL",null);
                Check(await bridge.Open(dll),"Reload after modal Stop failed.");
                delay=true;gate.Reset();var pending=Refresh();await Wait(()=>active==1);
                int pendingReads=reads;await Refresh();Check(reads==pendingReads&&peak==1,"Overlapping health requests accumulated.");
                Click("_stop");Check(await bridge.Open(dll),"Reload with old health read pending failed.");
                Check(Field<PluginWorkerHealth?>("_latestHealth")==null,"New worker inherited old health metrics.");
                gate.Set();await pending;Check(Field<PluginWorkerHealth?>("_latestHealth")==null,"Delayed old worker result published after Reload.");
                Check(history.Samples.Count==0,"Delayed old worker result repopulated replacement history.");
                delay=false;await Refresh();Check(state.Text.Contains(language[TextKey.PluginHealthResponding]),"New worker health did not recover.");
                var timer=Field<DispatcherTimer>("_healthTimer");bridge.Close();bridge=null;Check(!timer.IsEnabled,"Close retained the health timer.");
                Check(history.Samples.Count==0,"Close retained history.");
                language.Select("bn");Check(state.Text.Contains(new UiLanguage("en")[TextKey.PluginHealthStopped]),"Closed window retained language subscription.");
                Console.WriteLine("PASS: real worker CPU/RAM, unknown baseline, bilingual health UI, modal timer, unavailable-read recovery, coalescing, Stop/Reload stale-result exclusion and close cleanup.");result=0;
            }
            catch(Exception e){Console.Error.WriteLine(e);}
            finally{gate.Set();bridge?.Close();Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_MODAL",oldModal);app.Shutdown(result);}
        };
        app.Run();return result;
    }
    private static async Task Wait(Func<bool> condition){var watch=Stopwatch.StartNew();while(!condition()){if(watch.Elapsed>TimeSpan.FromSeconds(4))throw new TimeoutException("Health UI condition timed out.");await Task.Delay(20);}}

    internal static int Soak(string dll,int seconds,string report)
    {
        if(seconds is <30 or >900)throw new ArgumentOutOfRangeException(nameof(seconds));
        dll=Path.GetFullPath(dll);
        using var host=Process.GetCurrentProcess();var watch=Stopwatch.StartNew();int reads=0,reloads=0;var errors=new List<string>();
        var resources=new List<object>();
        host.Refresh();int coldHandles=host.HandleCount,handles=0;long memory=0;double warmup=0;PluginWorkerHealth? last=null;
        try
        {
            do
            {
                using var client=new PluginClient(Environment.ProcessPath!,dll);
                using var worker=Process.GetProcessById(client.WorkerId);
                Check(client.ReadHealth().CpuPercent==null,"Reload reused a CPU baseline.");
                for(int i=0;i<50;i++)
                {
                    client.Request(new("sample",[12345])).GetAwaiter().GetResult();last=client.ReadHealth();reads++;
                    Check(last.Availability==PluginWorkerAvailability.Available&&last.WorkingSetBytes>0&&last.CpuPercent is >=0 and <=100,"Health became invalid during sampling.");Thread.Sleep(100);
                }
                client.Dispose();Check(worker.WaitForExit(2000),"Health observer kept worker alive.");worker.Dispose();reloads++;
                host.Refresh();resources.Add(new{Reload=reloads,Seconds=watch.Elapsed.TotalSeconds,Handles=host.HandleCount,PrivateBytes=host.PrivateMemorySize64});
                // Warm native/process/async runtime caches across three full
                // worker lifecycles before gating retained-resource growth.
                if(reloads==3){handles=host.HandleCount;memory=host.PrivateMemorySize64;warmup=watch.Elapsed.TotalSeconds;}
            }while(reloads<3 || watch.Elapsed.TotalSeconds-warmup<seconds);
            host.Refresh();Check(host.HandleCount-handles<=32,"Health observers leaked host handles.");Check(host.PrivateMemorySize64-memory<=64L*1024*1024,"Health reads exceeded host memory growth budget.");
        }
        catch(Exception e){errors.Add(e.ToString());}
        host.Refresh();File.WriteAllText(report,JsonSerializer.Serialize(new{Format="TrafficSense.PluginHealthSoak",Version=1,AppVersion=typeof(App).Assembly.GetName().Version!.ToString(3),TestedAt=DateTimeOffset.UtcNow,TotalSeconds=watch.Elapsed.TotalSeconds,WarmupSeconds=warmup,MeasuredSeconds=watch.Elapsed.TotalSeconds-warmup,Reads=reads,Reloads=reloads,ColdHandles=coldHandles,WarmHandles=handles,HandleGrowth=host.HandleCount-handles,PrivateGrowthMiB=(host.PrivateMemorySize64-memory)/(1024d*1024),Resources=resources,Last=last,Errors=errors,Passed=errors.Count==0},new JsonSerializerOptions{WriteIndented=true}));
        foreach(string error in errors)Console.Error.WriteLine(error);return errors.Count==0?0:1;
    }
}
