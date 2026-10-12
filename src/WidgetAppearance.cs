namespace TrafficSense;

internal static class WidgetAppearance
{
    internal const int DefaultFontSize = 14;
    internal const int DefaultBackgroundOpacity = 120;
    internal static IReadOnlyList<int> FontSizes { get; } = [12, 14, 16, 18, 20, 24];
    internal static IReadOnlyList<int> BackgroundOpacities { get; } = [120, 255, 191, 128, 64, 26];

    internal static int NormalizeFontSize(int size) => FontSizes.Contains(size) ? size : DefaultFontSize;
    internal static int NormalizeBackgroundOpacity(int opacity) => BackgroundOpacities.Contains(opacity) ? opacity : DefaultBackgroundOpacity;
}
