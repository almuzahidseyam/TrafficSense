using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace TrafficSense;

internal sealed class PluginHealthGraph:FrameworkElement
{
    private PluginHealthPoint[] _points=[];
    private UiLanguage _language=new();
    internal void Update(IEnumerable<PluginHealthPoint> points,UiLanguage language,string current)
    {
        _points=points.ToArray();_language=language;
        AutomationProperties.SetName(this,language[TextKey.PluginHistory]+". "+current);
        AutomationProperties.SetHelpText(this,language[TextKey.PluginHistoryHint]);
        ToolTip=language[TextKey.PluginHistoryHint];InvalidateVisual();
    }
    protected override AutomationPeer OnCreateAutomationPeer()=>new GraphPeer(this);
    private sealed class GraphPeer(PluginHealthGraph owner):FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore()=>nameof(PluginHealthGraph);
        protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Image;
    }
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if(ActualWidth<180 || ActualHeight<150)return;
        double dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface=new Typeface("Segoe UI, Nirmala UI");
        void Label(string text,double x,double y)=>context.DrawText(new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,typeface,11,SystemColors.WindowTextBrush,dpi),new(x,y));
        double height=(ActualHeight-70)/2;
        var cpuPlot=new Rect(4,20,ActualWidth-8,height);
        var ramPlot=new Rect(4,50+height,ActualWidth-8,height);
        double maximumRam=Math.Max(1,Math.Ceiling(_points.Select(p=>p.WorkingSetBytes/(1024d*1024)??0).DefaultIfEmpty().Max()));
        Label(_language[TextKey.Cpu]+" · 0–100%",4,1);
        Label(_language[TextKey.Memory]+" · 0–"+maximumRam.ToString("0.#",CultureInfo.InvariantCulture)+" MiB",4,ramPlot.Top-17);
        Label("−120 s",4,ActualHeight-15);
        var now=new FormattedText(_language[TextKey.PluginHistoryNow],CultureInfo.CurrentCulture,FlowDirection.LeftToRight,typeface,11,SystemColors.WindowTextBrush,dpi);
        context.DrawText(now,new(Math.Max(4,ActualWidth-now.Width-4),ActualHeight-15));
        void Plot(Rect plot,Func<PluginHealthPoint,double?> value,double maximum,Brush brush,DashStyle dash)
        {
            context.DrawRectangle(SystemColors.WindowBrush,new Pen(SystemColors.GrayTextBrush,1),plot);
            context.DrawLine(new Pen(SystemColors.GrayTextBrush,.5){DashStyle=DashStyles.Dot},new(plot.Left,plot.Top+plot.Height/2),new(plot.Right,plot.Top+plot.Height/2));
            if(_points.Length==0)return;
            double end=_points[^1].Timestamp.TotalSeconds;
            var geometry=new StreamGeometry();var dots=new List<Point>();
            using(var drawing=geometry.Open())
            {
                TimeSpan? previous=null;
                foreach(var sample in _points)
                {
                    if(value(sample) is not {} measured){previous=null;continue;}
                    var point=new Point(plot.Left+(sample.Timestamp.TotalSeconds-end+120)/120*plot.Width,plot.Bottom-measured/maximum*plot.Height);
                    if(previous==null || sample.Timestamp-previous.Value>TimeSpan.FromSeconds(5))drawing.BeginFigure(point,false,false);
                    else drawing.LineTo(point,true,false);
                    dots.Add(point);previous=sample.Timestamp;
                }
            }
            geometry.Freeze();context.PushClip(new RectangleGeometry(plot));
            context.DrawGeometry(null,new Pen(brush,2){DashStyle=dash},geometry);
            foreach(var point in dots)context.DrawEllipse(brush,null,point,1.5,1.5);
            context.Pop();
        }
        Plot(cpuPlot,p=>p.CpuPercent,100,SystemParameters.HighContrast?SystemColors.WindowTextBrush:Brushes.ForestGreen,DashStyles.Solid);
        Plot(ramPlot,p=>p.WorkingSetBytes/(1024d*1024),maximumRam,SystemParameters.HighContrast?SystemColors.WindowTextBrush:Brushes.RoyalBlue,DashStyles.Dash);
        if(_points.Length==0)Label(_language[TextKey.PluginHistoryEmpty],12,cpuPlot.Top+8);
    }
}
