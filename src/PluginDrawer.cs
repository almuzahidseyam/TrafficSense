using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TrafficSense;

// Independent IPluginDrawer v1 adapter; only used in the disposable DLL worker.
internal sealed class PluginDrawer : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Api(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void TextCall(IntPtr self,int x,int y,int w,int h,IntPtr text,uint color,int align,[MarshalAs(UnmanagedType.U1)] bool multiline,byte alpha);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void RectCall(IntPtr self,int x,int y,int w,int h);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void FillCall(IntPtr self,int x,int y,int w,int h,uint color,byte alpha);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void OutlineCall(IntPtr self,int x,int y,int w,int h,uint color,int width,[MarshalAs(UnmanagedType.U1)] bool dotted,byte alpha,int radius);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void BitmapCall(IntPtr self,IntPtr bitmap,int x,int y,int w,int h,int stretch,byte alpha);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void IconCall(IntPtr self,IntPtr icon,int x,int y,int w,int h);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void ExtentCall(IntPtr self,IntPtr text,out int width,out int height);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void LineCall(IntPtr self,int x1,int y1,int x2,int y2,uint color,int width,[MarshalAs(UnmanagedType.U1)] bool dotted,byte alpha);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr DcCall(IntPtr self);
    private readonly IntPtr _dc, _table;
    private readonly Delegate[] _callbacks;
    internal IntPtr Pointer { get; }
    private Rectangle _clip;
    internal PluginDrawer(IntPtr dc,int width,int height)
    {
        _dc = dc; _clip = new(0,0,width,height);
        _callbacks = [new Api(_ => 1),
            new TextCall((_,x,y,w,h,text,color,align,multi,alpha) => Paint(g => {
                using var brush = new SolidBrush(ColorOf(color,alpha)); using var format = new StringFormat { Alignment = align == 1 ? StringAlignment.Far : align == 2 ? StringAlignment.Center : StringAlignment.Near,
                    FormatFlags = multi ? 0 : StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
                g.DrawString(ReadText(text),SystemFonts.MessageBoxFont!,brush,Rect(x,y,w,h),format); })),
            new RectCall((_,x,y,w,h) => _clip = Rectangle.Intersect(new(0,0,width,height),Rect(x,y,w,h))),
            new FillCall((_,x,y,w,h,color,alpha) => Paint(g => { using var brush = new SolidBrush(ColorOf(color,alpha)); g.FillRectangle(brush,Rect(x,y,w,h)); })),
            new OutlineCall((_,x,y,w,h,color,line,dotted,alpha,radius) => Paint(g => {
                using var pen = PenOf(color,line,dotted,alpha); var r = Rect(x,y,w,h);
                int size = Math.Clamp(radius,0,Math.Min(r.Width,r.Height)/2)*2;
                if (size < 2) g.DrawRectangle(pen,r); else { using var path = new GraphicsPath(); path.AddArc(r.Left,r.Top,size,size,180,90); path.AddArc(r.Right-size,r.Top,size,size,270,90); path.AddArc(r.Right-size,r.Bottom-size,size,size,0,90); path.AddArc(r.Left,r.Bottom-size,size,size,90,90); path.CloseFigure(); g.DrawPath(pen,path); } })),
            new BitmapCall((_,bitmap,x,y,w,h,stretch,alpha) => Paint(g => {
                using var image = Image.FromHbitmap(bitmap); var dest = Rect(x,y,w,h); using var attributes = new ImageAttributes(); attributes.SetColorMatrix(new ColorMatrix { Matrix33 = alpha/255f });
                var source = new Rectangle(0,0,image.Width,image.Height);
                if (stretch == 2) { double scale = Math.Min(dest.Width/(double)image.Width,dest.Height/(double)image.Height); int dw = (int)(image.Width*scale),dh = (int)(image.Height*scale); dest = new(dest.X+(dest.Width-dw)/2,dest.Y+(dest.Height-dh)/2,dw,dh); }
                if (stretch == 1 && dest.Width > 0 && dest.Height > 0) { double ratio = dest.Width/(double)dest.Height; if (image.Width/(double)image.Height > ratio) { int sw=(int)(image.Height*ratio); source = new((image.Width-sw)/2,0,sw,image.Height); } else { int sh=(int)(image.Width/ratio); source = new(0,(image.Height-sh)/2,image.Width,sh); } }
                g.DrawImage(image,dest,source.X,source.Y,source.Width,source.Height,GraphicsUnit.Pixel,attributes); })),
            new IconCall((_,icon,x,y,w,h) => Paint(g => { using var borrowed = Icon.FromHandle(icon); using var copy = (Icon)borrowed.Clone(); g.DrawIcon(copy,Rect(x,y,w,h)); })),
            new ExtentCall((IntPtr _,IntPtr text,out int w,out int h) => { int measuredW=0,measuredH=0; Paint(g => { var size=g.MeasureString(ReadText(text),SystemFonts.MessageBoxFont!); measuredW=(int)Math.Ceiling(size.Width); measuredH=(int)Math.Ceiling(size.Height); }); w=measuredW; h=measuredH; }),
            new LineCall((_,x1,y1,x2,y2,color,line,dotted,alpha) => Paint(g => { using var pen=PenOf(color,line,dotted,alpha); g.DrawLine(pen,Math.Clamp(x1,-4096,4096),Math.Clamp(y1,-4096,4096),Math.Clamp(x2,-4096,4096),Math.Clamp(y2,-4096,4096)); })),
            new DcCall(_ => _dc)];
        _table = Marshal.AllocHGlobal(_callbacks.Length*IntPtr.Size); Pointer = Marshal.AllocHGlobal(IntPtr.Size);
        for (int i=0;i<_callbacks.Length;i++) Marshal.WriteIntPtr(_table,i*IntPtr.Size,Marshal.GetFunctionPointerForDelegate(_callbacks[i])); Marshal.WriteIntPtr(Pointer,_table);
    }
    private void Paint(Action<Graphics> draw)
    { try { using var graphics=Graphics.FromHdc(_dc); graphics.SetClip(_clip); draw(graphics); } catch (Exception e) when (e is ArgumentException or ExternalException or InvalidOperationException) { } }
    private static Rectangle Rect(int x,int y,int w,int h) => new(Math.Clamp(x,-4096,4096),Math.Clamp(y,-4096,4096),Math.Clamp(w,0,4096),Math.Clamp(h,0,4096));
    private static Color ColorOf(uint color,byte alpha) => Color.FromArgb(alpha,(byte)color,(byte)(color>>8),(byte)(color>>16));
    private static Pen PenOf(uint color,int width,bool dotted,byte alpha) => new(ColorOf(color,alpha),Math.Clamp(width,1,64)) { DashStyle = dotted ? DashStyle.Dot : DashStyle.Solid };
    private static string ReadText(IntPtr pointer)
    { if(pointer==IntPtr.Zero) return ""; var chars=new List<char>(); for(int i=0;i<256;i++) { char c=(char)Marshal.ReadInt16(pointer,i*2); if(c==0)break; chars.Add(c); } return new(chars.ToArray()); }
    public void Dispose() { Marshal.FreeHGlobal(Pointer); Marshal.FreeHGlobal(_table); GC.KeepAlive(_callbacks); }
}
