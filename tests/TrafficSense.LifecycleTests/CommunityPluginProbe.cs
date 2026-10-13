using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
using TrafficSense;

internal static class CommunityPluginProbe
{
    internal sealed record Case(string Name,string Tag,string SourceRevision,string Source,string Release,string ArchiveSHA256,string DllSHA256,string Dll);
    private sealed record Resources(int Handles,uint Gdi,uint User);
    [DllImport("user32.dll")] private static extern uint GetGuiResources(nint process,uint kind);
    private static Resources Snapshot(Process worker){worker.Refresh();return new(worker.HandleCount,GetGuiResources(worker.Handle,0),GetGuiResources(worker.Handle,1));}
    internal static int Run(string manifestPath,string reportPath)
    {
        var cases=ReadCases(manifestPath);
        string root=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!,"plugin-probe-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var reports=new List<object>(); int failures=0;
        foreach(var test in cases)
        {
            string status="Passed"; string? error=null; var itemIds=new List<string>(); int api=0,drawn=0; bool changed=false; Resources? before=null,after=null; bool? gracefulConfig=null;
            try
            {
                string actual=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(test.Dll))).ToLowerInvariant();
                if(actual!=test.DllSHA256)throw new InvalidDataException("DLL differs from acquired manifest.");
                string configRoot=Path.Combine(root,test.Name);
                if(test.Name=="TextReader")
                {
                    string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(test.Dll))));
                    string config=Path.Combine(configRoot,hash); Directory.CreateDirectory(config); string sampleFile=Path.Combine(config,"sample.txt");
                    File.WriteAllText(sampleFile,"TrafficSense community compatibility sample — বাংলা পরীক্ষার লেখা।",new System.Text.UTF8Encoding(false));
                    File.WriteAllText(Path.Combine(config,Path.GetFileName(test.Dll)+".ini"),"[config]\nfile_path="+sampleFile+"\nauto_read=0\nauto_reload_when_file_changed=0\nuse_own_context_menu=0\n",System.Text.Encoding.Unicode);
                }
                using var client=new PluginClient(Environment.GetEnvironmentVariable("TRAFFICSENSE_PACKAGE_EXE") ?? Environment.ProcessPath!,test.Dll,configRoot);
                PluginSample? first=null;
                for(int i=0;i<4;i++)
                {
                    var sample=client.Request(new("sample",[12345,67890,20,30,10,-1,-1,-1,-1,5,3000,1000,2000],Dark:i%2==0)).GetAwaiter().GetResult();
                    if(sample.Items.Length==0 || sample.Items.Any(item=>string.IsNullOrWhiteSpace(item.Id)))throw new InvalidDataException("No identified plugin items.");
                    api=sample.Api; first??=sample;
                    if(i==0)itemIds.AddRange(sample.Items.Select(item=>item.Id));
                    if(!sample.Items.Select(item=>item.Id).SequenceEqual(itemIds))throw new InvalidDataException("Plugin item IDs changed unexpectedly.");
                    foreach(var item in sample.Items.Where(item=>item.Png!=null))
                    {
                        var bitmap=PluginImage.Decode(item.Png!); if(bitmap.PixelWidth<1 || bitmap.PixelHeight!=40)throw new InvalidDataException("Invalid custom preview."); drawn++;
                        if(i<2)File.WriteAllBytes(Path.Combine(root,test.Name+(i==0?"-dark":"-light")+".png"),Convert.FromBase64String(item.Png!));
                        var bgra=new System.Windows.Media.Imaging.FormatConvertedBitmap(bitmap,System.Windows.Media.PixelFormats.Bgra32,null,0);
                        var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4]; bgra.CopyPixels(pixels,bitmap.PixelWidth*4,0);
                        bool foreground=false;
                        for(int p=0;p<pixels.Length;p+=4)
                            if(pixels[p+3]>0 && (i%2==0 ? pixels[p]+pixels[p+1]+pixels[p+2]>60 : pixels[p]+pixels[p+1]+pixels[p+2]<700)){foreground=true;break;}
                        if(!foreground)throw new InvalidDataException("Custom preview contains no visible foreground.");
                    }
                    if(sample.Items.Select(item=>item.Value).SequenceEqual(first.Items.Select(item=>item.Value))==false)changed=true;
                    if(i<3)Thread.Sleep(1050);
                }
                if(test.Name=="DateTime" && !changed)throw new InvalidDataException("Clock never advanced across samples.");
                using var worker=Process.GetProcessById(client.WorkerId); before=Snapshot(worker);
                for(int i=0;i<32;i++)client.Request(new("sample",[12345,67890],Dark:i%2==0)).GetAwaiter().GetResult();
                after=Snapshot(worker);
                if(after.Gdi>before.Gdi+8 || after.User>before.User+8 || after.Handles>before.Handles+32)throw new InvalidDataException("Worker resource handles grew beyond the finite stress budget.");
                client.Dispose();
                if(test.Name=="DateTime")
                { gracefulConfig=Directory.GetFiles(configRoot,"DateTime.dll.ini",SearchOption.AllDirectories).Length==1; if(gracefulConfig!=true)throw new InvalidDataException("DateTime did not persist configuration on idle shutdown."); }
                Console.WriteLine($"PASS: community {test.Name} {test.Tag}, API {api}, {itemIds.Count} item(s), {drawn} valid dark/light custom previews.");
            }
            catch(Exception e) when(e is IOException or InvalidDataException or OperationCanceledException or InvalidOperationException or System.ComponentModel.Win32Exception or JsonException or ArgumentException or FormatException or NotSupportedException)
            { status="Failed"; error=e.GetType().Name+": "+e.Message; failures++; Console.WriteLine($"FAIL: {test.Name}: {error}"); }
            reports.Add(new {test.Name,test.Tag,test.SourceRevision,test.Source,test.Release,test.ArchiveSHA256,test.DllSHA256,Status=status,Error=error,Api=api,ItemIds=itemIds,CustomPreviewSamples=drawn,ValueChanged=changed,
                WarmResources=before,After32StressSamples=after,GracefulConfigurationSaved=gracefulConfig,
                Scope="Startup, repeated sampling, stable IDs, visible dark/light rendering, clock progress, seeded sample file for TextReader, 32 additional stress samples with native handle budgets; no options/commands/input events or hardware drivers."});
        }
        File.WriteAllText(reportPath,JsonSerializer.Serialize(new { Format="TrafficSense.CommunityPlugins",Version=1,TestedAt=DateTimeOffset.UtcNow,Windows=Environment.OSVersion.VersionString,Results=reports,
            Provenance="Binaries from author release assets; hashes record tested bytes, not reproducible-build/signature attestation. No third-party binaries bundled.",Configuration="Per-run isolated configuration directories; no user plugin configuration loaded."},new JsonSerializerOptions{WriteIndented=true}));
        return failures==0?0:1;
    }
    internal static Case[] ReadCases(string manifestPath)
    {
        var cases=JsonSerializer.Deserialize<Case[]>(LegacySkin.ReadBounded(manifestPath,65536),new JsonSerializerOptions{MaxDepth=8}) ?? throw new InvalidDataException("Missing plugin manifest.");
        if(cases.Length is <1 or >4 || cases.Select(c=>c.Name).Distinct().Count()!=cases.Length || cases.Any(c=>c.Name is not ("DateTime" or "Battery" or "KeyboardIndicator" or "TextReader")))throw new InvalidDataException("Unexpected community test cases.");
        foreach(var test in cases)
            if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(test.Dll))).ToLowerInvariant()!=test.DllSHA256)throw new InvalidDataException("DLL differs from acquired manifest.");
        return cases;
    }
}
