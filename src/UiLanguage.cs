namespace TrafficSense;

internal enum TextKey
{
    NetworkAdapter, StartWithWindows, SwitchTheme, Language, HideWidget, ShowWidget, Exit,
    AutoConnection, AutoDescription, UnavailableSuffix, MissingAdapter, ReconnectDescription,
    AutoPrefix, NoConnectedAdapter, WaitingForAdapter, ChooseAdapterHint, TrayHint,
    SettingsSaveFailed, StartupReadFailed, StartupChangeFailed, StartupNotApplied,
    StartFailure, UiCrash, BackgroundCrash, ErrorTitle, DownloadSpeed, UploadSpeed,
    FatalError, AppWillClose, LogSaved, LogUnavailable,
    About, Version, LogFolder, OpenLogFolder, LogFolderOpenFailed, Close,
    CheckUpdates, UpdateHint, UpdateChecking, UpdateAvailable, UpdateCurrent, UpdateNoRelease,
    UpdateRateLimited, UpdateCheckFailed, OpenRelease, OpenReleaseFailed,
    TextSize, BackgroundTransparency, DefaultAppearance, LockPosition, ResetPosition,
    SpeedUnit, RefreshInterval, BitsPerSecond, BytesPerSecond, Seconds,
    History, Today, ThisMonth, UsageHint, Download, Upload, GraphLegend
}

internal sealed class UiLanguage(string? code = "en")
{
    public string Code { get; private set; } = Normalize(code);
    public event EventHandler? Changed;

    public static string Normalize(string? code) => code == "bn" ? "bn" : "en";

    public string this[TextKey key] => Code == "bn" ? Messages[key].Bangla : Messages[key].English;

    public void Select(string? code)
    {
        string next = Normalize(code);
        if (Code == next) return;
        Code = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Adapter names, Windows diagnostics, and standard speed units are supplied
    // by the OS/measurement layer and are deliberately not translated here.
    private static readonly IReadOnlyDictionary<TextKey, (string English, string Bangla)> Messages =
        new Dictionary<TextKey, (string, string)>
        {
            [TextKey.NetworkAdapter] = ("Network adapter", "নেটওয়ার্ক অ্যাডাপ্টার"),
            [TextKey.StartWithWindows] = ("Start with Windows", "Windows চালু হলে চালু করুন"),
            [TextKey.SwitchTheme] = ("Switch Theme (Light/Dark)", "থিম বদলান (হালকা/গাঢ়)"),
            [TextKey.TextSize] = ("Text size", "লেখার আকার"),
            [TextKey.BackgroundTransparency] = ("Background transparency", "পটভূমির স্বচ্ছতা"),
            [TextKey.DefaultAppearance] = ("Default", "ডিফল্ট"),
            [TextKey.LockPosition] = ("Lock position", "অবস্থান লক করুন"),
            [TextKey.ResetPosition] = ("Reset position", "অবস্থান ফিরিয়ে আনুন"),
            [TextKey.SpeedUnit] = ("Speed unit", "গতির একক"),
            [TextKey.RefreshInterval] = ("Refresh interval", "গতি আপডেটের ব্যবধান"),
            [TextKey.BitsPerSecond] = ("Bits/s (Kbps, Mbps)", "বিট/সেকেন্ড (Kbps, Mbps)"),
            [TextKey.BytesPerSecond] = ("Bytes/s (KB/s, MB/s)", "বাইট/সেকেন্ড (KB/s, MB/s)"),
            [TextKey.Seconds] = ("{0} seconds", "{0} সেকেন্ড"),
            [TextKey.History] = ("Usage and history", "ডেটা ব্যবহার ও ইতিহাস"),
            [TextKey.Today] = ("Today", "আজ"),
            [TextKey.Download] = ("Download", "ডাউনলোড"),
            [TextKey.Upload] = ("Upload", "আপলোড"),
            [TextKey.GraphLegend] = ("Last 2 minutes — download: solid green; upload: dashed blue", "শেষ ২ মিনিট — ডাউনলোড: সবুজ অবিচ্ছিন্ন রেখা; আপলোড: নীল ভাঙা রেখা"),
            [TextKey.ThisMonth] = ("This month", "এই মাস"),
            [TextKey.UsageHint] = ("Measured traffic while TrafficSense is running, across your selected connections. Gaps during sleep, reconnects or app shutdown are excluded. Totals follow the local date; they are not your provider's billing totals.",
                "TrafficSense চালু থাকা অবস্থায় নির্বাচিত সংযোগগুলোর মাপা ডেটা। স্লিপ, পুনঃসংযোগ বা অ্যাপ বন্ধের বিরতি বাদ যায়। হিসাব স্থানীয় তারিখ অনুযায়ী; এটি ইন্টারনেট সেবাদাতার বিলের হিসাব নয়।"),
            [TextKey.Language] = ("Language", "ভাষা"),
            [TextKey.HideWidget] = ("Hide widget", "উইজেট লুকান"),
            [TextKey.ShowWidget] = ("Show widget", "উইজেট দেখান"),
            [TextKey.Exit] = ("Exit", "বন্ধ করুন"),
            [TextKey.About] = ("About TrafficSense", "TrafficSense সম্পর্কে"),
            [TextKey.Version] = ("Version", "সংস্করণ"),
            [TextKey.LogFolder] = ("Error log folder", "ত্রুটির লগ ফোল্ডার"),
            [TextKey.OpenLogFolder] = ("Open log folder", "লগ ফোল্ডার খুলুন"),
            [TextKey.LogFolderOpenFailed] = ("The log folder could not be opened. You can copy its address and try opening it in File Explorer.",
                "লগ ফোল্ডার খোলা যায়নি। ঠিকানাটি কপি করে File Explorer-এ খোলার চেষ্টা করতে পারেন।"),
            [TextKey.Close] = ("Close", "ফিরে যান"),
            [TextKey.CheckUpdates] = ("Check for updates", "আপডেট খুঁজুন"),
            [TextKey.UpdateHint] = ("Check GitHub for a newer version.", "GitHub-এ নতুন সংস্করণ খুঁজুন।"),
            [TextKey.UpdateChecking] = ("Checking for updates…", "নতুন আপডেট খোঁজা হচ্ছে…"),
            [TextKey.UpdateAvailable] = ("Version {0} is available.", "নতুন সংস্করণ {0} পাওয়া গেছে।"),
            [TextKey.UpdateCurrent] = ("You are up to date.", "আপনার সংস্করণ হালনাগাদ আছে।"),
            [TextKey.UpdateNoRelease] = ("No published release was found.", "কোনো প্রকাশিত রিলিজ পাওয়া যায়নি।"),
            [TextKey.UpdateRateLimited] = ("GitHub is limiting update checks. Please try again later.",
                "GitHub আপডেট খোঁজার অনুরোধ সীমিত করেছে। কিছুক্ষণ পরে চেষ্টা করুন।"),
            [TextKey.UpdateCheckFailed] = ("Could not check for updates. Check your connection and try again.",
                "আপডেট খোঁজা যায়নি। সংযোগ যাচাই করে আবার চেষ্টা করুন।"),
            [TextKey.OpenRelease] = ("View release and download", "রিলিজ দেখে ডাউনলোড করুন"),
            [TextKey.OpenReleaseFailed] = ("The release page could not be opened. Copy the address below into your browser.",
                "রিলিজ পৃষ্ঠা খোলা যায়নি। নিচের ঠিকানাটি কপি করে ব্রাউজারে খুলুন।"),
            [TextKey.AutoConnection] = ("Auto (one connection)", "স্বয়ংক্রিয় (একটি সংযোগ)"),
            [TextKey.AutoDescription] = ("Monitors one connected adapter automatically. VPN and underlying traffic are never added together.",
                "স্বয়ংক্রিয়ভাবে একটি সংযোগের গতি মাপে। VPN ও মূল সংযোগের ডেটা একসঙ্গে যোগ হয় না।"),
            [TextKey.UnavailableSuffix] = (" (disconnected/unavailable)", " (বিচ্ছিন্ন/অনুপলব্ধ)"),
            [TextKey.MissingAdapter] = ("Selected adapter (unavailable)", "নির্বাচিত অ্যাডাপ্টার (অনুপলব্ধ)"),
            [TextKey.ReconnectDescription] = ("This adapter will be monitored again when it reconnects. Choose Auto or another adapter to switch.",
                "এই অ্যাডাপ্টার আবার যুক্ত হলে গতি মাপা শুরু হবে। অন্য সংযোগ মাপতে স্বয়ংক্রিয় বা অন্য অ্যাডাপ্টার বেছে নিন।"),
            [TextKey.AutoPrefix] = ("Auto: ", "স্বয়ংক্রিয়: "),
            [TextKey.NoConnectedAdapter] = ("No connected network adapter", "কোনো নেটওয়ার্ক অ্যাডাপ্টার সংযুক্ত নেই"),
            [TextKey.WaitingForAdapter] = ("Selected adapter disconnected or unavailable. Waiting for it to reconnect.",
                "নির্বাচিত অ্যাডাপ্টার বিচ্ছিন্ন বা অনুপলব্ধ। আবার সংযুক্ত হওয়ার অপেক্ষায়।"),
            [TextKey.ChooseAdapterHint] = ("Right-click to choose a network adapter.", "অ্যাডাপ্টার বেছে নিতে ডান-ক্লিক করুন।"),
            [TextKey.TrayHint] = ("TrafficSense — double-click to show widget", "TrafficSense — উইজেট দেখতে ডাবল-ক্লিক করুন"),
            [TextKey.SettingsSaveFailed] = ("Your settings could not be saved.", "সেটিং সংরক্ষণ করা যায়নি।"),
            [TextKey.StartupReadFailed] = ("Windows startup settings could not be read.", "Windows-এ স্বয়ংক্রিয়ভাবে চালুর সেটিং পড়া যায়নি।"),
            [TextKey.StartupChangeFailed] = ("Windows startup settings could not be changed.", "Windows-এ স্বয়ংক্রিয়ভাবে চালুর সেটিং বদলানো যায়নি।"),
            [TextKey.StartupNotApplied] = ("Windows did not apply the startup setting. Please try again.", "Windows সেটিংটি প্রয়োগ করেনি। আবার চেষ্টা করুন।"),
            [TextKey.StartFailure] = ("TrafficSense could not start.", "TrafficSense চালু করা যায়নি।"),
            [TextKey.FatalError] = ("TrafficSense encountered an unexpected error.", "TrafficSense-এ অপ্রত্যাশিত ত্রুটি ঘটেছে।"),
            [TextKey.AppWillClose] = ("The app will close. You can start it again.", "অ্যাপটি বন্ধ হবে। আবার চালু করতে পারবেন।"),
            [TextKey.LogSaved] = ("Error details were saved here:", "ত্রুটির বিস্তারিত এখানে সংরক্ষিত হয়েছে:"),
            [TextKey.LogUnavailable] = ("Error details could not be saved.", "ত্রুটির বিস্তারিত সংরক্ষণ করা যায়নি।"),
            [TextKey.UiCrash] = ("UI error:", "ইন্টারফেসে ত্রুটি:"),
            [TextKey.BackgroundCrash] = ("Background error:", "পেছনে চলা কাজে ত্রুটি:"),
            [TextKey.ErrorTitle] = ("TrafficSense Error", "TrafficSense ত্রুটি"),
            [TextKey.DownloadSpeed] = ("Download speed", "ডাউনলোডের গতি"),
            [TextKey.UploadSpeed] = ("Upload speed", "আপলোডের গতি")
        };
}
