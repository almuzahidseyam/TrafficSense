using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;
using Forms = System.Windows.Forms;

namespace TrafficSense;

internal sealed record EmbeddedRequest(PixelRect Monitor, string Text, bool Light);
internal sealed record EmbeddedReply(bool Embedded, string Reason, string? Action = null);

internal sealed class EmbeddedTaskbarClient : IDisposable
{
    private readonly Process _process;
    private readonly WorkerJob _job;
    private int _disposed;
    internal EmbeddedTaskbarClient(bool inspectOnly = false)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true };
        start.ArgumentList.Add(inspectOnly ? "--taskbar-inspect" : "--taskbar-worker"); _process = Process.Start(start) ?? throw new IOException("Could not start taskbar worker.");
        try { _job = new(_process); } catch { try { _process.Kill(); } finally { _process.Dispose(); } throw; }
    }
    internal async Task<EmbeddedReply> Update(EmbeddedRequest request)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), timeout.Token).ConfigureAwait(false);
            await _process.StandardInput.FlushAsync(timeout.Token).ConfigureAwait(false);
            var chars = new List<char>(); var one = new char[1];
            while (true)
            {
                if (await _process.StandardOutput.ReadAsync(one, timeout.Token).ConfigureAwait(false) == 0) throw new EndOfStreamException();
                if (one[0] == '\n') break; if (chars.Count >= 4096) throw new InvalidDataException("Invalid taskbar response."); chars.Add(one[0]);
            }
            return JsonSerializer.Deserialize<EmbeddedReply>(new string(chars.ToArray())) ?? throw new InvalidDataException("Missing taskbar response.");
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _job.Dispose();
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        _process.Dispose();
    }
}

// Explorer hosts a child window owned by this isolated process, never the main
// WPF process. SetParent's cross-process DPI reset cannot alter the sampler UI.
internal static class EmbeddedTaskbarWorker
{
    internal static int Run(bool inspectOnly = false)
    {
        SetErrorMode(0x1 | 0x2 | 0x8000);
        using var input = new StreamReader(Console.OpenStandardInput()); using var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        using var form = new Forms.Form { FormBorderStyle = Forms.FormBorderStyle.None, ShowInTaskbar = false, TopMost = false, Text = "TrafficSense taskbar" };
        using var text = new Forms.Label { Dock = Forms.DockStyle.Fill, AutoEllipsis = true, TextAlign = System.Drawing.ContentAlignment.MiddleCenter, AccessibleName = "TrafficSense network speed" };
        using var font = new System.Drawing.Font("Segoe UI", 9); text.Font = font; form.Controls.Add(text);
        using var tooltip = new Forms.ToolTip(); string? action = null;
        text.MouseClick += (_, e) => action = e.Button == Forms.MouseButtons.Right ? "settings" : "history";
        _ = form.Handle;
        Task<string?> pending = ReadBounded(input);
        using var timer = new Forms.Timer { Interval = 50 };
        timer.Tick += (_, _) =>
        {
            if (!pending.IsCompleted) return;
            try
            {
                string? line = pending.GetAwaiter().GetResult(); if (line == null) { Forms.Application.ExitThread(); return; }
                var request = JsonSerializer.Deserialize<EmbeddedRequest>(line) ?? throw new InvalidDataException();
                var reply = Place(form, text, request, inspectOnly); tooltip.SetToolTip(text, request.Text);
                output.WriteLine(JsonSerializer.Serialize(reply with { Action = action })); action = null; pending = ReadBounded(input);
            }
            catch { Forms.Application.ExitThread(); }
        };
        timer.Start(); Forms.Application.Run(); return 0;
    }
    private static async Task<string?> ReadBounded(StreamReader input)
    {
        var chars = new List<char>(); var one = new char[1];
        while (await input.ReadAsync(one.AsMemory()).ConfigureAwait(false) != 0)
        { if (one[0] == '\n') return new string(chars.ToArray()); if (chars.Count >= 8192) throw new InvalidDataException(); chars.Add(one[0]); }
        return null;
    }
    private static EmbeddedReply Place(Forms.Form form, Forms.Label label, EmbeddedRequest request, bool inspectOnly)
    {
        form.Hide();
        if (request.Text.Length > 512) return new(false, "Text exceeds limit.");
        IntPtr host = IntPtr.Zero; PixelRect bar = default;
        EnumWindows((hwnd, _) =>
        {
            var name = new System.Text.StringBuilder(128); GetClassName(hwnd, name, name.Capacity);
            if (name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" && GetWindowRect(hwnd, out var rect) && rect.Intersects(request.Monitor))
            { host = hwnd; bar = rect; return false; } return true;
        }, IntPtr.Zero);
        if (host == IntPtr.Zero) return new(false, "Taskbar not found.");
        try
        {
            var root = AutomationElement.FromHandle(host); var elements = root.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition);
            if (elements.Count is < 2 or > 4096) return new(false, "Taskbar accessibility inventory unavailable.");
            var occupied = new List<PixelRect>(); bool shellButton = false;
            for (int i = 0; i < elements.Count; i++)
            {
                var current = elements[i].Current; if (current.IsOffscreen || current.NativeWindowHandle == form.Handle.ToInt32()) continue;
                var bounds = current.BoundingRectangle; if (bounds.IsEmpty || bounds.Width < 1 || bounds.Height < 1) continue;
                var kind = current.ControlType;
                // Ignore known structural containers only; unknown full-width
                // leaves make placement fail closed instead of guessing a gap.
                if (kind == ControlType.Pane || kind == ControlType.Group || kind == ControlType.Window || kind == ControlType.ToolBar) continue;
                var rectangle = new PixelRect((int)Math.Floor(bounds.Left), (int)Math.Floor(bounds.Top), (int)Math.Ceiling(bounds.Right), (int)Math.Ceiling(bounds.Bottom));
                occupied.Add(rectangle); if (kind == ControlType.Button && rectangle.Intersects(bar)) shellButton = true;
            }
            int dpi = (int)GetDpiForWindow(host); if (dpi is < 96 or > 768) dpi = 96;
            var target = TaskbarVacancy.Find(bar, occupied, 230 * dpi / 96, Math.Min(bar.Height, 32 * dpi / 96), shellButton);
            if (target == null) return new(false, "No verified vacant taskbar space; using edge panel.");
            if (inspectOnly) return new(false, "Read-only inspection: verified vacant space found; no window attached.");
            // WS_CHILD rather than WS_POPUP. Never inject or resize shell controls.
            var position = target.Value;
            label.Text = request.Text; label.AccessibleName = request.Text;
            label.BackColor = request.Light ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(24, 24, 24);
            label.ForeColor = request.Light ? System.Drawing.Color.Black : System.Drawing.Color.White;
            _ = label.Handle; SetWindowPos(label.Handle, IntPtr.Zero, 0, 0, position.Width, position.Height, 0x10 | 0x4 | 0x40);
            if (!Attach(form.Handle, host, position)) return new(false, "Taskbar attachment failed.");
            return new(true, "Embedded in verified vacant space.");
        }
        catch (Exception e) when (e is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        { return new(false, "Taskbar accessibility unavailable."); }
    }
    internal static bool Attach(IntPtr child, IntPtr host, PixelRect position)
    {
        nint style = GetWindowLongPtr(child, -16); SetWindowLongPtr(child, -16, (style & ~unchecked((nint)0x80000000u)) | 0x40000000);
        Marshal.SetLastPInvokeError(0); var old = SetParent(child, host);
        if (old == IntPtr.Zero && Marshal.GetLastPInvokeError() != 0) return false;
        var point = new Point { X = position.Left, Y = position.Top }; if (!ScreenToClient(host, ref point)) return false;
        return SetWindowPos(child, IntPtr.Zero, point.X, point.Y, position.Width, position.Height, 0x10 | 0x4 | 0x40);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    private delegate bool EnumProc(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int maximum);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out PixelRect rect);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr hwnd, ref Point point);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
}
