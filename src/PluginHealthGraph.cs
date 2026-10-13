using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;

namespace TrafficSense;

internal sealed class PluginHealthGraph:FrameworkElement
{
    private PluginHealthPoint[] _points=[];
    private UiLanguage _language=new();
    private readonly ToolTip _details=new();
    private PluginHealthPoint? _hover;
    internal PluginHealthPoint? HoveredPoint=>_hover;
    internal string HoverText=>_details.Content as string??"";
    public PluginHealthGraph()
    {
        ToolTip=_details;
        ToolTipService.SetShowDuration(this,int.MaxValue);
        Unloaded+=(_,_)=>Inspect(null);
    }
    internal void Update(IEnumerable<PluginHealthPoint> points,UiLanguage language,string current)
    {
        _points=points.ToArray();_language=language;
        AutomationProperties.SetName(this,language[TextKey.PluginHistory]+". "+current);
        AutomationProperties.SetHelpText(this,language[TextKey.PluginHistoryHint]);
        Inspect(IsMouseOver?Mouse.GetPosition(this):null);
    }
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);Inspect(e.GetPosition(this));}
    protected override void OnMouseLeave(MouseEventArgs e){base.OnMouseLeave(e);Inspect(null);}
    internal void Inspect(Point? position)
    {
        _hover=null;
        double height=(ActualHeight-70)/2;
        if(position is {} p && ActualWidth>=180 && ActualHeight>=150 && _points.Length>0 &&
            (new Rect(4,20,ActualWidth-8,height).Contains(p)||new Rect(4,50+height,ActualWidth-8,height).Contains(p)))
        {
            double target=_points[^1].Timestamp.TotalSeconds-120+(p.X-4)/(ActualWidth-8)*120;
            var nearest=_points.MinBy(s=>Math.Abs(s.Timestamp.TotalSeconds-target));
            // Do not extrapolate into empty history or bridge an unobserved delay.
            if(target>=_points[0].Timestamp.TotalSeconds-.000001 && Math.Abs(nearest.Timestamp.TotalSeconds-target)<=2.5)
                _hover=nearest;
            _details.Content=_hover is {} sample?FormatDetails(sample):_language[TextKey.PluginHistoryNoSample];
        }
        else
        {
            _details.Content=_language[TextKey.PluginHistoryHint];
            _details.IsOpen=false;
        }
        InvalidateVisual();
    }
    private string FormatDetails(PluginHealthPoint sample)
    {
        string time=$"{(long)sample.Timestamp.TotalHours:00}:{sample.Timestamp.Minutes:00}:{sample.Timestamp.Seconds:00}";
        string cpu=sample.CpuPercent is {} c?c.ToString("0.0",CultureInfo.InvariantCulture)+"%":_language[TextKey.NotAvailable];
        string ram=sample.WorkingSetBytes is {} r?(r/(1024d*1024)).ToString("0.0",CultureInfo.InvariantCulture)+" MiB":_language[TextKey.NotAvailable];
        return _language[TextKey.PluginHistorySessionTime]+": "+time+"\n"+_language[TextKey.Cpu]+": "+cpu+"\n"+_language[TextKey.Memory]+": "+ram;
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
        if(_hover is {} hovered && _points.Length>0)
        {
            double x=cpuPlot.Left+(hovered.Timestamp-_points[^1].Timestamp+PluginHealthHistory.Duration).TotalSeconds/120*cpuPlot.Width;
            var cursor=new Pen(SystemColors.WindowTextBrush,1){DashStyle=DashStyles.Dot};
            context.DrawLine(cursor,new(x,cpuPlot.Top),new(x,cpuPlot.Bottom));
            context.DrawLine(cursor,new(x,ramPlot.Top),new(x,ramPlot.Bottom));
        }
        if(_points.Length==0)Label(_language[TextKey.PluginHistoryEmpty],12,cpuPlot.Top+8);
    }
}
