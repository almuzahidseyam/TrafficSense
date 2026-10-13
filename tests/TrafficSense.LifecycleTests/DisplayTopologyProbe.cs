using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using TrafficSense;

internal static class DisplayTopologyProbe
{
    internal static int Run(string reportPath,bool requirePerMonitor=true)
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown}; int result=1;
        var window=new Window{Width=220,Height=70,Opacity=0,ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize};
        app.Startup+=(_,_)=>app.Dispatcher.BeginInvoke(new Action(()=>
        {
            try
            {
                window.Show(); nint handle=new WindowInteropHelper(window).Handle;
                nint context=GetWindowDpiAwarenessContext(handle); int awareness=GetAwarenessFromDpiAwarenessContext(context);
                bool v2=AreDpiAwarenessContextsEqual(context,new(-4));
                if(requirePerMonitor && awareness!=2)throw new InvalidOperationException("Apphost lacks per-monitor DPI awareness.");
                var primary=new DisplayWorkArea(0,0,1920,1040,true); var left=new DisplayWorkArea(-2560,-200,0,1200); var above=new DisplayWorkArea(0,-1440,2560,0);
                var cases=new List<object>();
                foreach(double sizeScale in new[]{1d,1.25,1.5,2d})
                {
                    // Scaled native window sizes and synthetic work areas exercise
                    // placement only. No fabricated WM_DPICHANGED or OS settings.
                    window.Width=220*sizeScale; window.Height=70*sizeScale; window.UpdateLayout();
                    var settings=new AppSettings{ScreenLeft=-2000,ScreenTop=100};
                    NativeWindowPlacement.Restore(window,settings,[left,primary,above]); var restored=Rect(handle);
                    Check(restored.Left==-2000 && restored.Top==100,"Valid negative monitor coordinates were lost.");
                    NativeWindowPlacement.EnsureVisible(window,[primary]); var removed=Rect(handle); Check(Inside(removed,primary),"Monitor removal left a native widget outside the remaining work area.");
                    var changed=new DisplayWorkArea(64,48,1280,720,true);
                    NativeWindowPlacement.EnsureVisible(window,[changed]); var reduced=Rect(handle); Check(Inside(reduced,changed),"Resolution/work-area reduction obscured the widget.");
                    NativeWindowPlacement.Restore(window,new(){ScreenLeft=200,ScreenTop=-1100},[primary,above]); var vertical=Rect(handle);
                    Check(vertical.Top==-1100 && Inside(vertical,above),"Monitor above primary lost its negative position.");
                    cases.Add(new{SimulatedSizeScale=sizeScale,Restored=restored,AfterMonitorRemoval=removed,AfterWorkAreaReduction=reduced,AbovePrimary=vertical});
                }
                var shell=new FakeShell(); var target=new DockBounds(-2560,-200,0,1200);
                var panel=new TaskbarPanelWindow(shell,()=>target,()=>{},()=>{},()=>{}){Opacity=0};
                var panels=new List<DockBounds>();
                try
                {
                    panel.Update("1 MB/s","2 KB/s","",new(),false,WidgetSkin.From(new()),false);
                    Check(panel.TryShow(),"Isolated panel did not register.");
                    Check(shell.Bounds.Left==target.Left && shell.Bounds.Right==target.Right && shell.Bounds.Bottom==target.Bottom,"Panel lost selected negative-coordinate monitor."); panels.Add(shell.Bounds);
                    target=new(0,0,1920,1080); panel.Reposition(); Drain();
                    Check(shell.Bounds.Left==0 && shell.Bounds.Bottom==1080,"Panel did not recover to remaining monitor.");panels.Add(shell.Bounds);
                    target=new(0,-1440,2560,0); panel.Update("1 MB/s","2 KB/s","",new(),true,WidgetSkin.From(new(){WidgetFontSize=24}),false);Drain();
                    Check(shell.Bounds.Top==-1440 && shell.Bounds.Right==2560 && shell.Bounds.Bottom<0,"Panel top/size change used the wrong monitor.");panels.Add(shell.Bounds);
                }
                finally{panel.ReleaseAndClose();}
                Check(shell.Removals==1,"Panel retained a simulated reservation after close.");
                File.WriteAllText(reportPath,JsonSerializer.Serialize(new{Format="TrafficSense.DisplayTopology",Version=1,TestedAt=DateTimeOffset.UtcNow,
                    Windows=Environment.OSVersion.VersionString,NativeDpiAwareness=awareness,PerMonitorV2=v2,ActualWindowDpi=GetDpiForWindow(handle),Cases=cases,
                    SimulatedPanelPositions=panels,
                    Scope="Actual invisible WPF window/native coordinates with injected work areas and scaled window sizes; no physical monitor/DPI setting change or fake WM_DPICHANGED payload.",
                    Untested="Physical second monitor, cross-device mixed-DPI transitions and monitor hot-plug."},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
                Console.WriteLine($"PASS: native DPI awareness={awareness}, PerMonitorV2={v2}; 4 synthetic scaled-size/display-topology sequences, negative coordinates, removal and work-area reduction."); result=0;
            }
            catch(Exception e){Console.Error.WriteLine(e);}
            finally{window.Close();app.Shutdown(result);}
        }));
        app.Run(); return result;
    }
    private static bool Inside(NativeRect r,DisplayWorkArea a)=>r.Left>=a.Left && r.Top>=a.Top && r.Right<=a.Right && r.Bottom<=a.Bottom;
    private static NativeRect Rect(nint hwnd){if(!GetWindowRect(hwnd,out var rect))throw new InvalidOperationException("Window bounds unavailable.");return rect;}
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Drain()=>Application.Current.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
    private sealed class FakeShell:IAppBarShell
    {
        internal DockBounds Bounds;internal int Removals;
        public bool Register(nint window,uint callback)=>true;
        public DockBounds Query(nint window,bool top,DockBounds bounds)=>bounds;
        public DockBounds Set(nint window,bool top,DockBounds bounds)=>bounds;
        public void Move(nint window,DockBounds bounds)=>Bounds=bounds;
        public void Notify(nint window,uint message){}
        public void Unregister(nint window)=>Removals++;
        public void FullScreen(nint window,bool active){}
        public void Arrange(nint window,bool hiding){}
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd,out NativeRect rect);
    [DllImport("user32.dll")] private static extern nint GetWindowDpiAwarenessContext(nint hwnd);
    [DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(nint left,nint right);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
}
