using System.Diagnostics;
using System.IO;
using TrafficSense;

internal static class PluginInteractionProbe
{
    internal static int Run(string dll)
    {
        dll=Path.GetFullPath(dll);
        string exe=Environment.GetEnvironmentVariable("TRAFFICSENSE_PACKAGE_EXE") ?? Environment.ProcessPath!;
        string root=Path.Combine(Path.GetTempPath(),"TrafficSense.Interactions-"+Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_MODAL","1");
        try
        {
            using var client=new PluginClient(exe,dll,root);
            var request=new PluginRequest("sample",[12345]);
            client.Request(request).GetAwaiter().GetResult();
            var options=client.Request(request with {Operation="options"}).GetAwaiter().GetResult();
            if(options.Tooltip!="options:1;commands:0")throw new InvalidDataException("Modal options callback was duplicated.");
            var command=client.Request(request with {Operation="command",Command=2}).GetAwaiter().GetResult();
            if(command.Tooltip!="options:1;commands:1")throw new InvalidDataException("Modal command callback was duplicated.");
            var pending=client.Request(request with {Operation="options"});
            try { client.Request(request).GetAwaiter().GetResult(); throw new InvalidOperationException("Concurrent request was accepted."); }
            catch(InvalidOperationException e) when(e.Message=="A plugin request is already running."){}
            if(pending.GetAwaiter().GetResult().Tooltip!="options:2;commands:1")throw new InvalidDataException("Request after concurrent rejection was lost.");
            Console.WriteLine("PASS: native modal message loops execute options and commands exactly once.");
            client.Dispose();
            Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_MODAL","2");
            string blocking=Path.Combine(root,"blocked");Directory.CreateDirectory(blocking);
            using var held=new PluginClient(exe,dll,blocking);
            held.Request(request).GetAwaiter().GetResult();
            var stopped=held.Request(request with {Operation="options"},TimeSpan.FromSeconds(10));
            var started=Stopwatch.StartNew();
            while(Directory.GetFiles(blocking,"interactive.txt",SearchOption.AllDirectories).Length==0)
            {if(started.Elapsed>TimeSpan.FromSeconds(3))throw new TimeoutException("Modal fixture did not start.");Thread.Sleep(10);}
            using var worker=Process.GetProcessById(held.WorkerId);var stop=Stopwatch.StartNew();held.Dispose();
            try{stopped.GetAwaiter().GetResult();throw new InvalidOperationException("Stopped modal callback returned success.");}
            catch(Exception e) when(e is IOException or ObjectDisposedException or OperationCanceledException){}
            if(!worker.WaitForExit(2000)||stop.Elapsed>TimeSpan.FromSeconds(3))throw new InvalidOperationException("Stop failed to remove a modal worker.");
            Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_MODAL",null);
            foreach(int invalid in new[]{-1,int.MaxValue})
            {
                using var rejected=new PluginClient(exe,dll,root);
                try{rejected.Request(request with {Operation="command",Command=invalid}).GetAwaiter().GetResult();throw new InvalidOperationException("Invalid command executed.");}
                catch(Exception e) when(e is IOException or InvalidDataException){}
            }
            Console.WriteLine("PASS: overlapping requests rejected, modal Stop cleans up, and invalid commands terminate without execution.");
            return 0;
        }
        finally{Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_MODAL",null);}
    }
    internal static int RunPrivateDesktop(string dll)
    {
        string root=Path.Combine(Path.GetTempPath(),"TrafficSense.PrivateDesktop-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string? old=Environment.GetEnvironmentVariable("TRAFFICSENSE_TEST_DIALOGS"),modal=Environment.GetEnvironmentVariable("TRAFFICSENSE_FIXTURE_MODAL");
        Environment.SetEnvironmentVariable("TRAFFICSENSE_TEST_DIALOGS","1");Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_MODAL","2");
        try
        {
            using var client=CommunityInteractionProbe.CreateClient(Path.GetFullPath(dll),root);var request=new PluginRequest("sample",[12345]);client.Request(request).GetAwaiter().GetResult();
            if(File.ReadAllText(Path.Combine(root,"desktop-ready"))!="private")throw new InvalidDataException("Private desktop was not verified in the child.");
            using var worker=CommunityInteractionProbe.OpenNativeWorker(root);using var launcher=Process.GetProcessById(client.WorkerId);
            var pending=client.Request(request with{Operation="options"},TimeSpan.FromSeconds(10));var started=Stopwatch.StartNew();
            while(Directory.GetFiles(root,"interactive.txt",SearchOption.AllDirectories).Length==0){if(started.Elapsed>TimeSpan.FromSeconds(3))throw new TimeoutException();Thread.Sleep(10);}
            client.Dispose();try{pending.GetAwaiter().GetResult();throw new InvalidOperationException("Stopped private callback returned success.");}catch(Exception e) when(e is IOException or ObjectDisposedException or OperationCanceledException){}
            if(!worker.WaitForExit(2000)||!launcher.WaitForExit(2000))throw new InvalidOperationException("Private desktop launcher/worker escaped job ownership.");
            Console.WriteLine("PASS: test-only private desktop identity, handshake after job assignment, and launcher/child cleanup during modal Stop.");return 0;
        }
        finally{Environment.SetEnvironmentVariable("TRAFFICSENSE_TEST_DIALOGS",old);Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_MODAL",modal);}
    }
}
