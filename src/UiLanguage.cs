namespace TrafficSense;

internal enum TextKey
{
    NetworkAdapter, StartWithWindows, SwitchTheme, Language, HideWidget, ShowWidget, Exit,
    AutoConnection, AutoDescription, UnavailableSuffix, MissingAdapter, ReconnectDescription,
    AutoPrefix, NoConnectedAdapter, WaitingForAdapter, ChooseAdapterHint, TrayHint,
    SettingsSaveFailed, StartupReadFailed, StartupChangeFailed, StartupNotApplied,
    StartFailure, UiCrash, BackgroundCrash, ErrorTitle, DownloadSpeed, UploadSpeed,
    FatalError, AppWillClose, LogSaved, LogUnavailable
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
            [TextKey.Language] = ("Language", "ভাষা"),
            [TextKey.HideWidget] = ("Hide widget", "উইজেট লুকান"),
            [TextKey.ShowWidget] = ("Show widget", "উইজেট দেখান"),
            [TextKey.Exit] = ("Exit", "বন্ধ করুন"),
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
