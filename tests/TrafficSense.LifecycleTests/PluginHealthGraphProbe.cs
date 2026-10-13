using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        if(output!=null){Directory.CreateDirectory(output);Save(bitmap,Path.Combine(output,"plugin-history-en.png"));}
        language.Select("bn");graph.Update(history.Samples,language,"CPU: ৫০% · RAM: ৫০ MiB");Render();
        Check(peer.GetName().Contains(language[TextKey.PluginHistory])&&peer.GetHelpText()==language[TextKey.PluginHistoryHint],"History accessibility did not translate.");
        if(output!=null)Save(bitmap,Path.Combine(output,"plugin-history-bn.png"));
        for(int i=12;i<512;i++)
        {
            history.Add(TimeSpan.FromSeconds(i),i%100,i*1024L*1024);graph.Update(history.Samples,language,"");Render();
        }
        Check(history.Samples.Count==121,"Rendered history did not expire old samples.");
        history.Clear();graph.Update(history.Samples,language,"");Render();
        Check(!Drawings(VisualTreeHelper.GetDrawing(graph)).OfType<GeometryDrawing>().Any(d=>d.Pen?.Thickness==2),"Cleared history retained a worker trace.");
        graph.Measure(new Size(220,180));graph.Arrange(new Rect(0,0,220,180));Render();
        Console.WriteLine("PASS: native CPU/RAM geometry, independent unknown gaps/scales, delayed gap, accessible bilingual labels, 500 bounded render updates, clear and narrow layout.");return 0;
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
