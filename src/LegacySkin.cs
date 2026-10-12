using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TrafficSense;

// Independent reader for the documented TrafficMonitor INI/XML interchange
// format. No upstream code, artwork or executable content is imported.
internal sealed record LegacySkinItem(string Key, string? PluginId, int X, int Y, int Width, int Align, string Label, uint Color);
internal sealed record LegacySkin(int Width, int Height, int TextHeight, string Font, double FontSize, int Style,
    bool NoLabel, string? Background, IReadOnlyList<LegacySkinItem> Items)
{
    internal static readonly string[] Keys = ["up", "down", "cpu", "memory", "gpu", "cpu_temperature", "gpu_temperature", "hdd_temperature", "main_board_temperature", "hdd", "total_speed", "cpu_freq", "today_traffic"];
    internal static string? LocalPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { return path.Length <= 1024 && Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\") ? Path.GetFullPath(path) : null; }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }
    internal static byte[] ReadBounded(string path, int limit)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > limit) throw new InvalidDataException("Skin file is too large.");
        using var output = new MemoryStream(); var buffer = new byte[4096]; int read;
        while ((read = input.Read(buffer)) != 0) { if (output.Length + read > limit) throw new InvalidDataException("Skin file is too large."); output.Write(buffer, 0, read); }
        return output.ToArray();
    }
    internal static LegacySkin Load(string path, bool compact)
    {
        if (LocalPath(path) == null) throw new InvalidDataException("Choose a local skin file.");
        var bytes = ReadBounded(path, 65536);
        using var text = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false, true), true);
        string content = text.ReadToEnd(); string suffix = compact ? "s" : "l";
        var items = new List<LegacySkinItem>(); int width, height, line, style; double size; string font; bool noLabel;
        static int Number(string? value, int fallback, int min, int max)
        { if (value == null) return fallback; if (min == 0 && max == 1 && bool.TryParse(value, out bool flag)) return flag ? 1 : 0; if (!int.TryParse(value, out int n) || n < min || n > max) throw new InvalidDataException("Invalid skin dimensions or options."); return n; }
        static string Label(string? value) { if (value?.Length > 128 || value?.Any(c => char.IsControl(c) && c != '\t') == true) throw new InvalidDataException("Invalid skin label."); return value ?? ""; }
        static uint[] Colors(string? value)
        { return (value ?? "0").Split(',').Select(v => uint.TryParse(v.Trim(), out var c) && c <= 0xffffff ? c : throw new InvalidDataException("Invalid skin color.")).Take(64).ToArray(); }
        if (Path.GetExtension(path).Equals(".xml", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = XmlReader.Create(new StringReader(content), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
            var root = XDocument.Load(reader).Root ?? throw new InvalidDataException("Missing skin root.");
            if (root.Descendants().Count() > 256 || root.Descendants().Any(e => e.Ancestors().Count() > 8)) throw new InvalidDataException("Skin is too complex.");
            var skin = root.Element("skin"); var layout = root.Element("layout") ?? throw new InvalidDataException("Missing layout.");
            var section = layout.Element("layout_" + suffix) ?? throw new InvalidDataException("Missing selected layout.");
            width = Number((string?)section.Attribute("width"), 220, 16, 2048); height = Number((string?)section.Attribute("height"), 43, 16, 2048);
            line = Number((string?)layout.Attribute("text_height"), 20, 8, 256); noLabel = Number((string?)layout.Attribute("no_label"), 0, 0, 1) == 1;
            font = WidgetSkin.Family((string?)skin?.Element("font")?.Attribute("name"));
            size = Number((string?)skin?.Element("font")?.Attribute("size"), 10, 6, 72) * 96d / 72;
            style = Number((string?)skin?.Element("font")?.Attribute("style"), 0, 0, 15);
            var colors = Colors((string?)skin?.Element("text_color")); bool each = Number((string?)skin?.Element("specify_each_item_color"), 0, 0, 1) == 1;
            var map = root.Element("plugin_map");
            foreach (var item in section.Elements())
            {
                if (items.Count >= 64) throw new InvalidDataException("Too many skin items.");
                string key = item.Name.LocalName; string? id = (string?)map?.Element(key);
                if (!Keys.Contains(key) && id == null) continue;
                if (id?.Length > 128) throw new InvalidDataException("Invalid plugin identifier.");
                if (Number((string?)item.Attribute("show"), 0, 0, 1) == 0) continue;
                int index = Array.IndexOf(Keys, key);
                items.Add(new(key, id, Number((string?)item.Attribute("x"), 0, -2048, 2048), Number((string?)item.Attribute("y"), 0, -2048, 2048),
                    Number((string?)item.Attribute("width"), 100, 1, 2048), Number((string?)item.Attribute("align"), 0, 0, 3),
                    Label((string?)skin?.Element("display_text")?.Element(key)), colors[each && index >= 0 && index < colors.Length ? index : 0]));
            }
        }
        else if (Path.GetExtension(path).Equals(".ini", StringComparison.OrdinalIgnoreCase))
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); string section = "";
            foreach (string raw in content.Split('\n'))
            {
                string row = raw.Trim(); if (row.Length == 0 || row[0] is ';' or '#') continue;
                if (row.StartsWith('[') && row.EndsWith(']')) { section = row[1..^1]; continue; }
                int split = row.IndexOf('='); if (split <= 0) throw new InvalidDataException("Invalid INI row.");
                values[section + "." + row[..split].Trim()] = row[(split + 1)..].Trim().Trim('"');
            }
            string? Get(string sectionName, string key) => values.GetValueOrDefault(sectionName + "." + key);
            int Layout(string key, int fallback, int min, int max) => Number(Get("layout", key), fallback, min, max);
            width = Layout("width_" + suffix, 220, 16, 2048); height = Layout("height_" + suffix, compact ? 28 : 43, 16, 2048);
            line = Layout("text_height", 20, 8, 256); noLabel = Layout("no_text", 0, 0, 1) == 1;
            font = WidgetSkin.Family(Get("skin", "font_name")); size = Number(Get("skin", "font_size"), 10, 6, 72) * 96d / 72;
            style = Number(Get("skin", "font_style"), 0, 0, 15); var colors = Colors(Get("skin", "text_color")); bool each = Number(Get("skin", "specify_each_item_color"), 0, 0, 1) == 1;
            string[] keys = ["up", "down", "cpu", "memory"];
            for (int index = 0; index < keys.Length; index++)
            {
                string key = keys[index]; if (Layout("show_" + key + "_" + suffix, compact && index > 1 ? 0 : 1, 0, 1) == 0) continue;
                items.Add(new(key, null, Layout(key + "_x_" + suffix, index % 2 == 0 ? 6 : 114, -2048, 2048), Layout(key + "_y_" + suffix, index < 2 ? 2 : 21, -2048, 2048),
                    Layout(key + "_width_" + suffix, 104, 1, 2048), Layout(key + "_align_" + suffix, 0, 0, 3), Label(Get("skin", key + "_string")), colors[each ? Math.Min(index, colors.Length - 1) : 0]));
            }
        }
        else throw new InvalidDataException("Choose skin.ini or skin.xml.");
        if (items.Count == 0) throw new InvalidDataException("Skin contains no visible supported items.");
        string directory = Path.GetDirectoryName(path)!; string name = compact ? "background" : "background_l";
        string? background = new[] { Path.Combine(directory, name + ".png"), Path.Combine(directory, name + ".bmp") }.FirstOrDefault(File.Exists);
        return new(width, height, line, font, size, style, noLabel, background, items);
    }
}
