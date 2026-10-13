using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TrafficSense;

internal static class PluginBridgeUiProbe
{
    internal static int Run(string dll)
    {
        dll=Path.GetFullPath(dll);string root=Path.Combine(Path.GetTempPath(),"TrafficSense.BridgeUi-"+Guid.NewGuid().ToString("N"));
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;var workers=new List<int>();PluginBridgeWindow? bridge=null;
        Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_MODAL","1");
        app.Startup+=async(_,_)=>
        {
            try
            {
                bridge=new(new UiLanguage("en"),()=>[12345],_=>{},()=>false,path=>{var client=new PluginClient(Environment.ProcessPath!,path,root);workers.Add(client.WorkerId);return client;}){Opacity=0,ShowActivated=false,ShowInTaskbar=false};
                bridge.Show();Check(await bridge.Open(dll),"UI bridge did not open its fixture.");
                T Field<T>(string name)=>(T)typeof(PluginBridgeWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(bridge)!;
                var options=Field<Button>("_options");var stop=Field<Button>("_stop");var run=Field<Button>("_run");var commands=Field<ComboBox>("_commands");var status=Field<TextBlock>("_status");
                var reload=Field<Button>("_reload");Check(reload.Content?.ToString()==new UiLanguage("en")[TextKey.PluginReload],"Reload control is not localized.");
                commands.SelectedIndex=2;Check(run.IsEnabled&&options.IsEnabled&&stop.IsEnabled,"Loaded command controls are disabled.");
                int beats=0;using var heartbeat=new System.Windows.Forms.Timer{Interval=20};heartbeat.Tick+=(_,_)=>beats++;heartbeat.Start();
                options.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!options.IsEnabled&&!run.IsEnabled&&stop.IsEnabled,"Interactive busy controls are incorrect.");
                await Wait(()=>status.Text.Contains("options:1;commands:0")&&options.IsEnabled);Check(beats>=3,"Plugin modal callback blocked the host UI.");
                run.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>status.Text.Contains("options:1;commands:1")&&run.IsEnabled);
                options.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));stop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Wait(()=>!Field<bool>("_busy"));Check(!stop.IsEnabled&&!run.IsEnabled&&commands.Items.Count==0,"Stop did not clear bridge state.");
                using(var prior=TryProcess(workers[0]))Check(prior==null||prior.WaitForExit(2000),"UI Stop left a native worker alive.");
                reload.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>workers.Count==2&&run.IsEnabled);Check(workers[0]!=workers[1],"UI Reload did not replace the stopped worker.");
                commands.SelectedIndex=3;run.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Wait(()=>status.Text==new UiLanguage("en")[TextKey.PluginResourceLimit]&&!Field<bool>("_busy"));Check(reload.IsEnabled&&!stop.IsEnabled&&!run.IsEnabled,"Resource fault recovery controls are incorrect.");
                reload.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>workers.Count==3&&run.IsEnabled);
                bridge.Close();bridge=null;using(var latest=TryProcess(workers[2]))Check(latest==null||latest.WaitForExit(2000),"Closing bridge left a native worker alive.");
                Console.WriteLine("PASS: actual WPF bridge buttons, selected command, responsive modal settings, Stop/Reload, resource-limit recovery and close cleanup.");result=0;
            }
            catch(Exception e){Console.Error.WriteLine(e);}
            finally{bridge?.Close();Environment.SetEnvironmentVariable("TRAFFICSENSE_FIXTURE_MODAL",null);app.Shutdown(result);}
        };
        app.Run();return result;
    }
    private static Process? TryProcess(int id){try{return Process.GetProcessById(id);}catch(ArgumentException){return null;}}
    private static async Task Wait(Func<bool> condition){var watch=Stopwatch.StartNew();while(!condition()){if(watch.Elapsed>TimeSpan.FromSeconds(6))throw new TimeoutException("Bridge UI condition timed out.");await Task.Delay(20);}}
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
