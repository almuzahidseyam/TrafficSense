using System.Diagnostics;
using System.IO;
using TrafficSense;

internal static class PluginProbe
{
    internal static int Run(string dll)
    {
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        var request = new PluginRequest("sample", [12345]);
        string executable = Environment.GetEnvironmentVariable("TRAFFICSENSE_PACKAGE_EXE") ?? Environment.ProcessPath!;
        for(int api=1;api<=7;api++)
        {
            Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_API",api.ToString());
            try { using var legacy = new PluginClient(executable,dll); var value=legacy.Request(request).GetAwaiter().GetResult(); Check(value.Api==api && value.Items[0].Value==(api>=7?"verified":"legacy") && value.Commands.Length==(api>=5?4:0),"Legacy API gate/monitor info failed."); }
            finally { Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_API",null); }
        }
        Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_API","9");
        try { using var unknown = new PluginClient(executable,dll); try { unknown.Request(request).GetAwaiter().GetResult(); throw new InvalidOperationException("Unknown API loaded."); } catch(InvalidDataException) {} }
        finally { Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_API",null); }
        using (var client = new PluginClient(executable, dll))
        {
            var sample = client.Request(request).GetAwaiter().GetResult();
            Check(sample.Api == 8 && sample.Items.Length == 3 && sample.Items[0].Value == "verified" && sample.Items[1].Png?.Length > 20 && sample.Commands.Length == 4, "Native ABI/host callbacks/custom draw failed.");
            foreach (var item in sample.Items.Skip(1))
            { using var bytes = new MemoryStream(Convert.FromBase64String(item.Png!)); using var bitmap = new System.Drawing.Bitmap(bytes); Check(bitmap.GetPixel(60,30).G > 100,"Plugin drawing was blank."); }
            Check(client.Request(request with { Operation = "options" }).GetAwaiter().GetResult().Name == sample.Name, "Plugin options failed.");
            Check(client.Request(request with { Operation = "command", Command = 2 }).GetAwaiter().GetResult().Items[0].Value == "verified", "Plugin command failed.");
            Check(client.Request(request with { Dark = false }).GetAwaiter().GetResult().Items[0].Value == "verified", "Light-theme GDI state/host color failed.");
        }
        foreach (int command in new[] { 0, 1 })
        {
            using var client = new PluginClient(executable, dll);
            client.Request(request).GetAwaiter().GetResult(); var watch = Stopwatch.StartNew(); bool failed = false;
            try { client.Request(request with { Operation = "command", Command = command }, TimeSpan.FromMilliseconds(500)).GetAwaiter().GetResult(); }
            catch (Exception e) when (e is OperationCanceledException or IOException) { failed = true; }
            Check(failed && watch.Elapsed < TimeSpan.FromSeconds(4), "Hung/crashed DLL escaped its deadline.");
        }
        using (var client = new PluginClient(executable, Path.Combine(Path.GetDirectoryName(dll)!, "missing.dll")))
        { try { client.Request(request).GetAwaiter().GetResult(); throw new InvalidOperationException("Missing DLL loaded."); } catch (Exception e) when (e is IOException or InvalidDataException) { } }
        string teardownRoot=Path.Combine(Path.GetTempPath(),"TrafficSense.Teardown-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(teardownRoot);
        using(var clean=new PluginClient(executable,dll,teardownRoot))clean.Request(request).GetAwaiter().GetResult();
        Check(Directory.GetFiles(teardownRoot,"saved.txt",SearchOption.AllDirectories).Length==1,"Idle shutdown skipped native DLL settings teardown.");
        string report = Path.Combine(Path.GetTempPath(), "TrafficSense.worker." + Guid.NewGuid().ToString("N") + ".txt");
        var ownerStart = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false, CreateNoWindow=true };
        ownerStart.ArgumentList.Add("--plugin-job-owner"); ownerStart.ArgumentList.Add(dll); ownerStart.ArgumentList.Add(report);
        using (var owner = Process.Start(ownerStart)!)
        {
            Check(owner.WaitForExit(5000) && owner.ExitCode == 0,"Worker owner fixture failed.");
            int worker = int.Parse(File.ReadAllText(report)); bool exited;
            try { using var orphan = Process.GetProcessById(worker); exited = orphan.WaitForExit(3000); } catch (ArgumentException) { exited=true; }
            Check(exited,"Worker survived abrupt owner exit."); File.Delete(report);
        }
        Console.WriteLine("PASS: independent x64 native ABI, retained host callbacks, text/GDI drawing, options/commands, missing DLL, crash and hung-worker deadline containment.");
        return 0;
    }
    internal static int RunJobOwner(string dll,string report)
    {
        var client = new PluginClient(Environment.ProcessPath!,dll);
        client.Request(new("sample",[12345])).GetAwaiter().GetResult();
        _ = client.Request(new("command",[12345],0),TimeSpan.FromMinutes(5)); File.WriteAllText(report,client.WorkerId.ToString());
        Environment.Exit(0); return 1; // Deliberately bypass IDisposable; kernel job must own teardown.
    }
}
