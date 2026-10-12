using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TrafficSense;
using Forms = System.Windows.Forms;

internal static class CompatibilityProbe
{
    internal static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "TrafficSense.Compatibility", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var store = new SettingsStore(Path.Combine(root,"settings.json")); bool fail = false; var errors = new List<string>();
        var app = new App(@"Local\TrafficSense.Compatibility." + Guid.NewGuid().ToString("N"), () => new MainWindow(new(),
            s => fail ? new(false,"blocked") : store.Save(s), errors.Add, usageStore: new(Path.Combine(root,"usage.json")), readAdapters: () => [], sampleTimestamp: () => TimeSpan.Zero, appBarShell: new FakeShell()));
        app.InitializeComponent(); var style = new Style(typeof(MainWindow)); style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
        style.Setters.Add(new Setter(Window.ShowActivatedProperty, false)); app.Resources.Add(typeof(MainWindow), style); bool passed = false;
        var panelStyle = new Style(typeof(TaskbarPanelWindow)); panelStyle.Setters.Add(new Setter(UIElement.OpacityProperty,0d)); app.Resources.Add(typeof(TaskbarPanelWindow),panelStyle);
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var window = (MainWindow)app.MainWindow;
                string ini = Path.Combine(root,"skin.ini");
                File.WriteAllText(ini,"[skin]\nfont_name=Consolas\nfont_size=12\nfont_style=4\ntext_color=255\ndown_string=Download: \n[layout]\nwidth_l=250\nheight_l=64\ntext_height=24\nwidth_s=180\nheight_s=32\nshow_cpu_l=0\nshow_memory_l=0\n");
                byte[] pixels = new byte[4 * 16 * 16]; for (int i = 0; i < pixels.Length; i+=4) { pixels[i] = 100; pixels[i+3] = 128; }
                var image = BitmapSource.Create(16,16,96,96,PixelFormats.Bgra32,null,pixels,64); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using (var output = File.Create(Path.Combine(root,"background_l.png"))) encoder.Save(output);
                Check(window.ChangeLayoutSkin(ini,false,Path.Combine(root,"managed")), "Bitmap layout import failed.");
                var canvas = (Canvas)window.FindName("LayoutCanvas"); Check(canvas.Width == 250 && canvas.Height == 64 && canvas.Children.Count == 2 && ((Grid)window.FindName("NormalSurface")).Visibility == Visibility.Collapsed, "Positioned skin not rendered.");
                var bitmap = (BitmapSource)((ImageBrush)canvas.Background).ImageSource; var bgra = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32,null,0); var copy = new byte[1024]; bgra.CopyPixels(copy,64,0); Check(copy[3] == 128, "PNG alpha lost.");
                Check(((TextBlock)window.FindName("LayoutStatus")).Visibility == Visibility.Visible, "Unknown sample indicator hidden by skin.");
                string saved = File.ReadAllText(Path.Combine(root,"settings.json"));
                fail = true; Check(!window.ChangeLayoutSkin(null,false) && canvas.Width == 250, "Failed save changed active layout."); fail = false;
                File.WriteAllText(ini,"[layout]\nwidth_l=999999\n"); Check(!window.ChangeLayoutSkin(ini,false,Path.Combine(root,"managed")) && File.ReadAllText(Path.Combine(root,"settings.json")) == saved, "Malformed skin changed saved settings.");
                string managed = store.Load().LayoutSkinFile!; File.Delete(Path.Combine(root,"background_l.png"));
                Check(window.ChangeLayoutSkin(managed,true,Path.Combine(root,"managed")) && canvas.Width == 180, "Managed asset or compact layout failed.");
                string backup = Path.Combine(root,"backup.json"); Check(window.ExportSettings(backup) && window.ChangeLayoutSkin(null,false) && window.RestoreSettings(backup), "Skin backup/restore failed.");
                app.Dispatcher.Invoke(() => {}, DispatcherPriority.ContextIdle);
                Check(canvas.Width == 180 && ((StackPanel)window.FindName("LayoutSurface")).Visibility == Visibility.Visible, "Restored layout not applied.");
                Check(window.ChangeLayoutSkin(null,false) && ((Grid)window.FindName("NormalSurface")).Visibility == Visibility.Visible, "Standard widget fallback failed.");
                Check(window.ChangeEmbedding(true) && store.Load().TaskbarPanel && store.Load().EmbedTaskbar,"Choosing embedding did not enable a taskbar display.");
                fail = true; Check(!window.ChangeEmbedding(false) && store.Load().EmbedTaskbar,"Failed embedding save changed preference."); fail = false;
                Check(window.ChangeEmbedding(false) && store.Load().TaskbarPanel && !store.Load().EmbedTaskbar,"Disabling embedding did not retain edge fallback.");
                window.ChangeTaskbarPanel(false);
                using var parent = new Forms.Form { ShowInTaskbar = false, Opacity = 0 }; using var child = new Forms.Form { ShowInTaskbar = false, Opacity = 0 };
                var rect = parent.RectangleToScreen(new System.Drawing.Rectangle(8,8,230,32));
                Check(EmbeddedTaskbarWorker.Attach(child.Handle,parent.Handle,new(rect.Left,rect.Top,rect.Right,rect.Bottom)), "Native child attachment failed.");
                Check(GetParent(child.Handle) == parent.Handle, "Native child parent was not set.");
                window.Close(); passed = true;
            }
            catch (Exception e) { Console.Error.WriteLine(e); app.Shutdown(1); }
        }));
        int exit = app.Run(); Check(passed && exit == 0,"Compatibility probe failed.");
        Console.WriteLine("PASS: native positioned INI rendering, PNG alpha, missing-sample status, managed assets, compact switch, malformed/save rollback, backup/restore and isolated native child attachment."); return 0;
    }
    private static void Check(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hwnd);
    private sealed class FakeShell : IAppBarShell
    {
        public bool Register(IntPtr w,uint c)=>true;
        public DockBounds Query(IntPtr w,bool t,DockBounds b)=>b;
        public DockBounds Set(IntPtr w,bool t,DockBounds b)=>b;
        public void Move(IntPtr w,DockBounds b){}
        public void Notify(IntPtr w,uint m){}
        public void Unregister(IntPtr w){}
        public void FullScreen(IntPtr w,bool a){}
        public void Arrange(IntPtr w,bool h){}
    }
}
