using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using TrafficSense;

internal static class PluginHealthGraphProbe
{
    internal static int Run(string? output=null)
    {
        var history=new PluginHealthHistory();
        void Add(int second,double? cpu,long? mib)=>history.Add(TimeSpan.FromSeconds(second),cpu,mib*1024*1024);
        Add(0,10,10);Add(1,20,20);Add(2,null,25);Add(3,30,-1);Add(4,40,40);Add(11,50,50);
        var language=new UiLanguage("en");var graph=new PluginHealthGraph();
        graph.Update(history.Samples,language,"CPU: 50% · RAM: 50 MiB");
        graph.Measure(new Size(560,180));graph.Arrange(new Rect(0,0,560,180));
        var bitmap=new RenderTargetBitmap(560,180,96,96,PixelFormats.Pbgra32);
        void Render(){graph.UpdateLayout();bitmap.Clear();bitmap.Render(graph);}
        Render();
        var lines=Drawings(VisualTreeHelper.GetDrawing(graph)).OfType<GeometryDrawing>().Where(d=>d.Pen?.Thickness==2).ToArray();
        Check(lines.Length==2,"History did not render two independent traces.");
        var cpu=PathGeometry.CreateFromGeometry(lines[0].Geometry);var ram=PathGeometry.CreateFromGeometry(lines[1].Geometry);
        Check(cpu.Figures.Count==3&&ram.Figures.Count==3,"Unknown values or long gaps were connected.");
        Check(cpu.Figures[1].StartPoint.X<ram.Figures[1].StartPoint.X,"Missing CPU and RAM readings did not break their traces independently.");
        Check(Math.Abs(lines[0].Geometry.Bounds.Height*2-lines[1].Geometry.Bounds.Height)<.01,"CPU fixed scale or independent RAM scale is incorrect.");
        var peer=UIElementAutomationPeer.CreatePeerForElement(graph)!;
        Check(peer.GetAutomationControlType()==AutomationControlType.Image&&peer.GetName().Contains(language[TextKey.PluginHistory])&&peer.GetHelpText().Contains("Gaps"),"History has no accessible name/scope.");
        Point At(double second,double y=40)=>new(4+(second-11+120)/120*552,y);
        graph.Inspect(At(2));
        Check(graph.HoveredPoint?.Timestamp==TimeSpan.FromSeconds(2)&&graph.HoverText.Contains("00:00:02")&&graph.HoverText.Contains("Unavailable")&&graph.HoverText.Contains("25.0 MiB"),"Hover lost timestamp or independent missing CPU.");
        graph.Inspect(At(3,120));
        Check(graph.HoverText.Contains("30.0%")&&graph.HoverText.Contains("Unavailable"),"RAM plot hover lost independent missing RAM.");
        graph.Inspect(At(1.4));Check(graph.HoveredPoint?.Timestamp==TimeSpan.FromSeconds(1),"Hover did not select the nearest actual reading.");
        graph.Inspect(At(7));Check(graph.HoveredPoint==null&&graph.HoverText==language[TextKey.PluginHistoryNoSample],"Hover invented a reading inside a long delay.");
        graph.Inspect(At(-1));Check(graph.HoveredPoint==null,"Hover extrapolated before history began.");
        graph.Inspect(At(11));Check(graph.HoveredPoint?.Timestamp==TimeSpan.FromSeconds(11),"Right-edge reading is inaccessible.");
        graph.Inspect(new Point(560,40));Check(graph.HoveredPoint==null,"Hover escaped the plot boundary.");
        graph.Inspect(At(4,85));Check(graph.HoveredPoint==null,"Hover selected a reading between plots.");
        graph.Inspect(At(4));graph.Inspect(null);Check(graph.HoveredPoint==null,"Mouse leave retained a hover marker.");
        if(output!=null){Directory.CreateDirectory(output);Save(bitmap,Path.Combine(output,"plugin-history-en.png"));}
        language.Select("bn");graph.Update(history.Samples,language,"CPU: ৫০% · RAM: ৫০ MiB");Render();
        Check(peer.GetName().Contains(language[TextKey.PluginHistory])&&peer.GetHelpText().Contains(language[TextKey.PluginHistoryHint])&&peer.GetHelpText().Contains(language[TextKey.PluginHistoryKeyboard]),"History accessibility did not translate.");
        graph.Inspect(At(2));Check(graph.HoverText.Contains(language[TextKey.PluginHistorySessionTime])&&graph.HoverText.Contains(language[TextKey.NotAvailable]),"Hover did not translate.");
        if(output!=null)Save(bitmap,Path.Combine(output,"plugin-history-bn.png"));
        Check(graph.Focusable&&KeyboardNavigation.GetIsTabStop(graph),"Graph is absent from keyboard navigation.");
        bool Key(Key key)=>graph.Navigate(key,ModifierKeys.None);
        Check(Key(System.Windows.Input.Key.End)&&graph.HoveredPoint?.Timestamp==TimeSpan.FromSeconds(11),"End did not select latest.");
        Key(System.Windows.Input.Key.Left);Check(graph.HoveredPoint?.Timestamp==TimeSpan.FromSeconds(4),"Left did not traverse recorded samples across a gap.");
        graph.Inspect(At(0));Check(graph.HoveredPoint?.Timestamp==TimeSpan.FromSeconds(4),"Mouse movement replaced keyboard selection.");
        graph.Update(history.Samples,language,"");Check(graph.HoveredPoint?.Timestamp==TimeSpan.FromSeconds(4),"Refresh replaced keyboard selection.");
        language.Select("en");graph.Update(history.Samples,language,"");
        Check(graph.KeyboardDetails.Contains("00:00:04")&&peer.GetName().Contains(graph.KeyboardDetails),"Selected sample is not accessible or translated.");
        Key(System.Windows.Input.Key.Home);Key(System.Windows.Input.Key.Left);Check(graph.HoveredPoint?.Timestamp==TimeSpan.Zero,"Left wrapped past oldest sample.");
        Key(System.Windows.Input.Key.Right);Key(System.Windows.Input.Key.Right);Check(graph.KeyboardDetails.Contains("Unavailable")&&graph.KeyboardDetails.Contains("25.0 MiB"),"Keyboard lost independent unknown values.");
        Key(System.Windows.Input.Key.End);Key(System.Windows.Input.Key.Right);Check(graph.HoveredPoint?.Timestamp==TimeSpan.FromSeconds(11),"Right wrapped past latest sample.");
        Check(!Key(System.Windows.Input.Key.Tab)&&!Key(System.Windows.Input.Key.Up)&&!graph.Navigate(System.Windows.Input.Key.Left,ModifierKeys.Control),"Graph trapped Tab, scrolling or modified shortcuts.");
        Key(System.Windows.Input.Key.Escape);Check(graph.HoveredPoint==null&&graph.KeyboardDetails==language[TextKey.PluginHistoryKeyboard],"Escape retained selection.");
        Key(System.Windows.Input.Key.Home);
        for(int i=12;i<512;i++)
        {
            history.Add(TimeSpan.FromSeconds(i),i%100,i*1024L*1024);graph.Update(history.Samples,language,"");Render();
        }
        Check(history.Samples.Count==121,"Rendered history did not expire old samples.");
        Check(graph.KeyboardDetails==language[TextKey.PluginHistoryKeyboard],"Expired selection silently jumped to another sample.");
        Key(System.Windows.Input.Key.End);
        history.Clear();graph.Update(history.Samples,language,"");Render();
        Check(graph.HoveredPoint==null&&!graph.HoverText.Contains("00:00:02"),"Clear retained stale hover details.");
        Check(!Drawings(VisualTreeHelper.GetDrawing(graph)).OfType<GeometryDrawing>().Any(d=>d.Pen?.Thickness==2),"Cleared history retained a worker trace.");
        history.Add(TimeSpan.FromHours(25),0,0);graph.Update(history.Samples,language,"");graph.Inspect(new Point(556,40));
        Check(graph.HoverText.Contains("25:00:00")&&graph.HoverText.Contains("0.0%")&&graph.HoverText.Contains("0.0 MiB"),"Hover wrapped session hours or hid genuine zero.");
        history.Clear();graph.Update(history.Samples,language,"");
        Key(System.Windows.Input.Key.End);Check(graph.HoveredPoint==null,"Empty history fabricated a keyboard sample.");
        graph.Measure(new Size(220,180));graph.Arrange(new Rect(0,0,220,180));Render();
        Console.WriteLine("PASS: native CPU/RAM geometry, bilingual hover time/values, nearest sample, unknown/zero, gaps/bounds/reset, independent scales, 500 bounded render updates and narrow layout.");return 0;
    }
    private static void Save(BitmapSource bitmap,string path)
    {
        // The control inherits its window background in the app. Supply that
        // same background when capturing it without a containing window.
        var visual=new DrawingVisual();
        using(var drawing=visual.RenderOpen()){drawing.DrawRectangle(SystemColors.WindowBrush,null,new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight));drawing.DrawImage(bitmap,new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight));}
        var opaque=new RenderTargetBitmap(bitmap.PixelWidth,bitmap.PixelHeight,96,96,PixelFormats.Pbgra32);opaque.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(opaque));using var stream=File.Create(path);encoder.Save(stream);
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static IEnumerable<Drawing> Drawings(Drawing drawing)
    {
        yield return drawing;
        if(drawing is DrawingGroup group)foreach(var child in group.Children)foreach(var nested in Drawings(child))yield return nested;
    }
}
