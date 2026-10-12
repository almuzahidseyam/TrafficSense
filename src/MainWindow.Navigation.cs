using System.Windows;
using System.Windows.Controls;

namespace TrafficSense;

public partial class MainWindow
{
    private readonly List<(MenuItem Menu, TextKey Key)> _groups = [];
    private void InitializeNavigation()
    {
        var menu = ((Border)Content).ContextMenu;
        menu.Items.Clear();
        void Group(TextKey key, params MenuItem[] children)
        {
            var group = new MenuItem(); foreach (var child in children) group.Items.Add(child);
            _groups.Add((group,key)); menu.Items.Add(group);
        }
        Group(TextKey.ConnectionGroup, MenuNetworkAdapter,MenuSpeedUnit,MenuRefreshInterval);
        Group(TextKey.AppearanceGroup, MenuPositionLock,MenuResetPosition,MenuTheme,MenuFontSize,MenuTransparency,MenuSkinPresets,MenuFontFamily,MenuColors,MenuSkinImport,MenuSkinExport,MenuLayoutSkin);
        Group(TextKey.MetricsGroup, MenuMetrics,MenuSystemDetails,MenuSensorSource);
        Group(TextKey.UsageGroup, MenuDailyLimit,MenuMonthlyLimit);
        Group(TextKey.PanelGroup, MenuTaskbarPanel,MenuEmbedTaskbar,MenuPanelTop,MenuPanelMonitor);
        Group(TextKey.PreferencesGroup, MenuAutoStart,MenuShortcut,MenuLanguage,MenuSettingsBackup);
        menu.Items.Add(new Separator()); menu.Items.Add(MenuHistory); menu.Items.Add(MenuAbout); menu.Items.Add(MenuHide); menu.Items.Add(MenuExit);
    }
    private void ApplyNavigationLanguage() { foreach (var (menu,key) in _groups) menu.Header = Localization[key]; }
}
