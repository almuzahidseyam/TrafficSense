using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using TrafficSense;
using Forms=System.Windows.Forms;

// Test executable only: real community dialogs run on a private desktop which
// is never made visible. The product worker has no dialog automation path.
internal static class PluginDialogDriver
{
    internal sealed record Plan(string Id,string Mode);
    internal sealed record Observation(string Id,string Mode,int RootDialogs,int ChildControls,double OpenMilliseconds);
    private delegate bool EnumWindow(nint window,nint data);
    internal static int RunWorker(string dll)
    {
        string root=Environment.GetEnvironmentVariable("TRAFFICSENSE_PLUGIN_CONFIG_ROOT") ?? throw new InvalidOperationException("Isolated dialog configuration required.");
        var desktopName=new StringBuilder(256);
        if(!GetUserObjectInformation(GetThreadDesktop(GetCurrentThreadId()),2,desktopName,512,out _) || !desktopName.ToString().StartsWith("TrafficSense.Test.",StringComparison.Ordinal))throw new InvalidOperationException("Dialog automation requires the test launcher's private desktop.");
        File.WriteAllText(Path.Combine(root,"desktop-ready"),"private");
        {
            using var timer=new Forms.Timer{Interval=50}; string? handled=null,observed=null; long opened=0; bool acting=false;
            timer.Tick+=(_,_)=>
            {
                if(acting)return;
                string path=Path.Combine(root,"dialog-plan.json"); if(!File.Exists(path))return;
                Plan plan;try{plan=JsonSerializer.Deserialize<Plan>(LegacySkin.ReadBounded(path,4096))!;}catch(IOException){return;}
                if(plan.Id==handled)return;
                var dialogs=new List<nint>();
                EnumThreadWindows(GetCurrentThreadId(),(window,_)=>
                {var name=new StringBuilder(64);GetClassName(window,name,64);if(name.ToString()=="#32770" && GetDlgItem(window,2)!=0)dialogs.Add(window);return true;},0);
                if(dialogs.Count==0)return;
                if(dialogs.Count!=1)throw new InvalidOperationException("Duplicate root plugin dialogs.");
                if(observed!=plan.Id){observed=plan.Id;opened=Stopwatch.GetTimestamp();}
                double milliseconds=Stopwatch.GetElapsedTime(opened).TotalMilliseconds;
                int controls=0; EnumChildWindows(dialogs[0],(_,_)=>{controls++;return true;},0);
                if(plan.Mode=="Hold")
                {Write(root,new(plan.Id,plan.Mode,dialogs.Count,controls,milliseconds));handled=plan.Id;return;}
                if(milliseconds<400)return; // Exercise several nested worker timer ticks.
                acting=true;
                try
                {
                    if(plan.Mode is "DateTimeAccept" or "DateTimeCancel")
                    {
                        nint edit=GetDlgItem(dialogs[0],1003);if(edit==0)throw new InvalidOperationException("Reviewed DateTime edit control missing.");
                        SendText(edit,0x000C,0,"ZZZ");
                    }
                    handled=plan.Id; Write(root,new(plan.Id,plan.Mode,dialogs.Count,controls,milliseconds));
                    // Only this test thread's private-desktop dialog receives input.
                    SendMessage(dialogs[0],0x0111,plan.Mode is "Accept" or "DateTimeAccept" ? 1 : 2,0);
                }
                finally{acting=false;}
            };
            timer.Start();return PluginWorker.Run(dll);
        }
    }
    private static void Write(string root,Observation observation)=>File.WriteAllText(Path.Combine(root,"dialog-observed.json"),JsonSerializer.Serialize(observation));
    [DllImport("user32.dll")]private static extern nint GetThreadDesktop(uint thread);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool GetUserObjectInformation(nint handle,int index,StringBuilder name,uint length,out uint required);
    [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]private static extern bool EnumThreadWindows(uint thread,EnumWindow callback,nint data);
    [DllImport("user32.dll")]private static extern bool EnumChildWindows(nint window,EnumWindow callback,nint data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(nint window,StringBuilder name,int size);
    [DllImport("user32.dll")]private static extern nint GetDlgItem(nint window,int id);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SendMessageW")]private static extern nint SendText(nint window,uint message,nint wparam,string text);
    [DllImport("user32.dll",EntryPoint="SendMessageW")]private static extern nint SendMessage(nint window,uint message,nint wparam,nint lparam);
}
