using TrafficSense;
using Xunit;

public sealed class LegacySkinTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "TrafficSense.SkinTests", Guid.NewGuid().ToString("N"));
    public LegacySkinTests() => Directory.CreateDirectory(root);
    private string Write(string name, string text) { string path = Path.Combine(root, name); File.WriteAllText(path, text); return path; }
    [Fact] public void IniReadsSeparateLayoutsAndLabels()
    {
        string path = Write("skin.ini", "[skin]\nfont_name=Consolas\nfont_size=12\nfont_style=3\nup_string=আপলোড: \ntext_color=255,65280\nspecify_each_item_color=1\n[layout]\nwidth_l=300\nheight_l=80\nwidth_s=200\nheight_s=30\nup_x_l=8\nup_align_l=3\n");
        var large = LegacySkin.Load(path, false); var small = LegacySkin.Load(path, true);
        Assert.Equal(300, large.Width); Assert.Equal(200, small.Width); Assert.Equal(4, large.Items.Count); Assert.Equal(2, small.Items.Count);
        Assert.Equal(16, large.FontSize); Assert.Equal(3, large.Style); Assert.Equal(255u, large.Items[0].Color);
        Assert.Equal(65280u, large.Items[1].Color); Assert.StartsWith("আপলোড", large.Items[0].Label); Assert.Equal(3, large.Items[0].Align);
    }
    [Fact] public void XmlPluginMappingAndPngPreference()
    {
        string path = Write("skin.xml", "<root><skin><font name='Consolas' size='9' style='4'/><display_text><down>↓ </down></display_text></skin><plugin_map><temperature>cpu-id</temperature></plugin_map><layout text_height='24' no_label='0'><layout_l width='240' height='64'><down x='12' y='2' width='100' align='2' show='1'/><temperature x='10' y='25' width='200' show='1'/></layout_l></layout></root>");
        Write("background_l.bmp", "unused"); Write("background_l.png", "unused"); var skin = LegacySkin.Load(path, false);
        Assert.EndsWith("background_l.png", skin.Background); Assert.Equal("cpu-id", skin.Items[1].PluginId); Assert.Equal("↓ ", skin.Items[0].Label); Assert.Equal(24, skin.TextHeight);
    }
    [Theory] [InlineData("<root><layout><layout_l width='2147483647'/></layout></root>")]
    [InlineData("<!DOCTYPE root [<!ENTITY x SYSTEM 'file:///C:/private'>]><root>&x;</root>")]
    [InlineData("<root><layout><layout_l width='200' height='30'><down show='1' align='9'/></layout_l></layout></root>")]
    public void RejectsMalformedOrOversizedXml(string text) => Assert.ThrowsAny<Exception>(() => LegacySkin.Load(Write("skin.xml", text), false));
    [Fact] public void BoundedReadAndLocalPaths()
    {
        Assert.Null(LegacySkin.LocalPath("https://example.com/skin.ini")); Assert.Null(LegacySkin.LocalPath(@"\\server\skin.ini"));
        Assert.Throws<InvalidDataException>(() => LegacySkin.Load(Write("skin.ini", new string('x', 65537)), false));
    }
    public void Dispose() { foreach (string path in Directory.GetFiles(root)) File.Delete(path); Directory.Delete(root); }
}
