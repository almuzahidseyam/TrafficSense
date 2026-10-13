using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using TrafficSense;

internal static class CommunityInteractionProbe
{
    private sealed record Resources(int Handles,uint Gdi,uint User);
    private static Resources Capture(Process process){process.Refresh();return new(process.HandleCount,GetGuiResources(process.Handle,0),GetGuiResources(process.Handle,1));}
    internal static int Run(string manifest,string reportPath)
    {
        var results=new List<object>();var cases=CommunityPluginProbe.ReadCases(manifest);
        string root=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!,"interactions-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string? previous=Environment.GetEnvironmentVariable("TRAFFICSENSE_TEST_DIALOGS");Environment.SetEnvironmentVariable("TRAFFICSENSE_TEST_DIALOGS","1");
        try
        {
            foreach(var test in cases)
            {
                string config=Path.Combine(root,test.Name);Directory.CreateDirectory(config);if(test.Name=="TextReader")SeedText(test.Dll,config);
                var actions=new List<PluginDialogDriver.Observation>();var request=new PluginRequest("sample",[12345]);
                Resources? beforeDialogs=null,afterDialogs=null;
                using(var client=CreateClient(test.Dll,config))
                {
                    var first=client.Request(request).GetAwaiter().GetResult();
                    using var native=OpenNativeWorker(config);beforeDialogs=Capture(native);
                    var cancel=Dialog(client,config,request with {Operation="options"},test.Name=="DateTime"?"DateTimeCancel":"Cancel",actions);
                    if(test.Name=="DateTime")Check(cancel.Items.All(i=>i.Value!="ZZZ"),"DateTime Cancel committed an edited format.");
                    var accept=Dialog(client,config,request with {Operation="options"},test.Name=="DateTime"?"DateTimeAccept":"Accept",actions);
                    if(test.Name=="DateTime")Check(accept.Items.Any(i=>i.Value=="ZZZ"),"DateTime accepted format did not reach the next sample.");
                    Check(accept.Items.Select(i=>i.Id).SequenceEqual(first.Items.Select(i=>i.Id)),"Item IDs changed after settings.");
                    if(test.Name=="TextReader")
                    {
                        Check(first.Commands.Length==7,"Reviewed TextReader command map changed.");
                        string? before=accept.Items[0].Png;
                        var next=client.Request(request with {Operation="command",Command=1}).GetAwaiter().GetResult();Check(next.Items[0].Png!=before,"Next page did not change the sample image.");
                        var back=client.Request(request with {Operation="command",Command=0}).GetAwaiter().GetResult();Check(back.Items[0].Png==before,"Previous page did not restore the sample image.");
                        client.Request(request with {Operation="command",Command=2}).GetAwaiter().GetResult();
                        client.Request(request with {Operation="command",Command=3}).GetAwaiter().GetResult();
                        client.Request(request with {Operation="command",Command=3}).GetAwaiter().GetResult();
                        var hidden=client.Request(request with {Operation="command",Command=4}).GetAwaiter().GetResult();Check(hidden.Items[0].Png!=before,"Hide command did not alter rendering.");
                        var shown=client.Request(request with {Operation="command",Command=4}).GetAwaiter().GetResult();Check(shown.Items[0].Png==before,"Hide toggle did not restore rendering.");
                        foreach(int command in new[]{5,6})Dialog(client,config,request with {Operation="command",Command=command},"Cancel",actions);
                    }
                    afterDialogs=Capture(native);
                    Console.WriteLine($"RESOURCES: {test.Name}, GDI {beforeDialogs.Gdi}->{afterDialogs.Gdi}, USER {beforeDialogs.User}->{afterDialogs.User}, handles {beforeDialogs.Handles}->{afterDialogs.Handles}.");
                }
                using(var reload=CreateClient(test.Dll,config))
                {
                    var sample=reload.Request(request).GetAwaiter().GetResult();
                    if(test.Name=="DateTime")Check(sample.Items.Any(i=>i.Value=="ZZZ"),"DateTime format was lost across reload.");
                    string id=SetPlan(config,"Hold");var pending=reload.Request(request with {Operation="options"},TimeSpan.FromSeconds(10));
                    WaitObserved(config,id);using var worker=Process.GetProcessById(reload.WorkerId);using var nativeWorker=OpenNativeWorker(config);var watch=Stopwatch.StartNew();reload.Dispose();
                    try{pending.GetAwaiter().GetResult();throw new InvalidOperationException("Stopped dialog returned a successful response.");}
                    catch(Exception e) when(e is IOException or ObjectDisposedException or OperationCanceledException){}
                    Check(worker.WaitForExit(2000)&&nativeWorker.WaitForExit(2000)&&watch.Elapsed<TimeSpan.FromSeconds(3),"Stop left the modal worker or its launcher alive.");
                }
                using(var reopen=CreateClient(test.Dll,config))Check(reopen.Request(request).GetAwaiter().GetResult().Items.Length>0,"Reopen after Stop failed.");
                Check(Directory.GetFiles(config,"*.ini",SearchOption.AllDirectories).Length>0,"Plugin did not persist an isolated configuration.");
                results.Add(new{test.Name,test.Tag,test.DllSHA256,Passed=true,Actions=actions,BeforeDialogs=beforeDialogs,AfterDialogs=afterDialogs,DateTimeEditedFormatPersisted=test.Name=="DateTime",CommandsExercised=test.Name=="TextReader"?Enumerable.Range(0,7).ToArray():Array.Empty<int>(),StopDuringDialog=true,ReopenAfterStop=true});
                Console.WriteLine($"PASS: {test.Name} private-desktop Cancel/OK, persistence, modal Stop and reopen; {actions.Count} observed dialogs.");
            }
            File.WriteAllText(reportPath,JsonSerializer.Serialize(new{Format="TrafficSense.CommunityInteractions",Version=1,TestedAt=DateTimeOffset.UtcNow,Results=results,
                Scope="Real pinned author-release dialogs on private desktops which were never switched into view; isolated plugin configuration and owned TextReader document. No user input, user documents or global key state changed. Not all option fields/hardware callbacks tested."},new JsonSerializerOptions{WriteIndented=true}));return 0;
        }
        finally{Environment.SetEnvironmentVariable("TRAFFICSENSE_TEST_DIALOGS",previous);}
    }
    internal static PluginSample Dialog(PluginClient client,string config,PluginRequest request,string mode,List<PluginDialogDriver.Observation> observations)
    {string id=SetPlan(config,mode);var sample=client.Request(request,TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();var seen=ReadObserved(config,id);Check(seen.OpenMilliseconds>=400 && seen.RootDialogs==1 && seen.ChildControls>0,"Native dialog was not exercised.");observations.Add(seen);return sample;}
    internal static string SetPlan(string root,string mode)
    {
        string id=Guid.NewGuid().ToString("N"),temp=Path.Combine(root,id+".json"),path=Path.Combine(root,"dialog-plan.json");
        File.WriteAllText(temp,JsonSerializer.Serialize(new PluginDialogDriver.Plan(id,mode)));
        for(int attempt=0;;attempt++){try{File.Move(temp,path,true);break;}catch(IOException) when(attempt<10){Thread.Sleep(10);}}
        return id;
    }
    internal static int TraceDialogResources(string manifest,string reportPath)
    {
        var results=new List<object>();string root=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!,"dialog-resources-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("TRAFFICSENSE_TEST_DIALOGS","1");
        foreach(var test in CommunityPluginProbe.ReadCases(manifest))
        {
            string config=Path.Combine(root,test.Name);Directory.CreateDirectory(config);if(test.Name=="TextReader")SeedText(test.Dll,config);
            using var client=CreateClient(test.Dll,config);client.Request(new("sample",[12345])).GetAwaiter().GetResult();using var worker=OpenNativeWorker(config);
            var resources=new List<Resources>{Capture(worker)};
            for(int i=0;i<8;i++){Dialog(client,config,new("options",[12345]),"Cancel",[]);resources.Add(Capture(worker));}
            Console.WriteLine($"TRACE: {test.Name}; GDI {string.Join(",",resources.Select(r=>r.Gdi))}; USER {string.Join(",",resources.Select(r=>r.User))}; handles {string.Join(",",resources.Select(r=>r.Handles))}.");
            results.Add(new{test.Name,Resources=resources});
        }
        File.WriteAllText(reportPath,JsonSerializer.Serialize(new{ObservationOnly=true,Results=results},new JsonSerializerOptions{WriteIndented=true}));return 0;
    }
    internal static PluginClient CreateClient(string dll,string config)
    {
        string launcher=Environment.GetEnvironmentVariable("TRAFFICSENSE_TEST_LAUNCHER") ?? throw new InvalidOperationException("Private desktop launcher required.");
        string go=Path.Combine(config,"launcher-go");File.Delete(go);
        Environment.SetEnvironmentVariable("TRAFFICSENSE_TEST_PROBE_EXE",Environment.ProcessPath!);
        var client=new PluginClient(launcher,dll,config);
        try{File.WriteAllText(go,"assigned");return client;}catch{client.Dispose();throw;}
    }
    internal static Process OpenNativeWorker(string config)=>Process.GetProcessById(int.Parse(File.ReadAllText(Path.Combine(config,"worker-pid.txt"),Encoding.Unicode)));
    private static PluginDialogDriver.Observation ReadObserved(string root,string id)
    {var result=JsonSerializer.Deserialize<PluginDialogDriver.Observation>(File.ReadAllText(Path.Combine(root,"dialog-observed.json")))!;Check(result.Id==id,"Expected dialog was not observed.");return result;}
    private static void WaitObserved(string root,string id)
    {var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(5)){try{if(ReadObserved(root,id).Id==id)return;}catch(Exception e) when(e is IOException or JsonException or InvalidOperationException){}Thread.Sleep(20);}throw new TimeoutException("Owned plugin dialog did not appear.");}
    internal static void SeedText(string dll,string root)
    {
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dll))));string config=Path.Combine(root,hash);Directory.CreateDirectory(config);string document=Path.Combine(config,"sample.txt");
        File.WriteAllText(document,string.Join("\n",Enumerable.Range(1,200).Select(i=>$"Chapter {i}: Owned compatibility sample — বাংলা পরীক্ষার লেখা {i}.")),new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(config,"TextReader.dll.ini"),"[config]\nfile_path="+document+"\nauto_read=0\nauto_reload_when_file_changed=0\nuse_own_context_menu=0\n",Encoding.Unicode);
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    [DllImport("user32.dll")]private static extern uint GetGuiResources(nint process,uint flags);
}
