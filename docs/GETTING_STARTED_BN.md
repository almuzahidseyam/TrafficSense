# TrafficSense — বাংলা নির্দেশনা

TrafficSense Windows-এ download/upload গতি, CPU/RAM এবং ডেটা ব্যবহারের হিসাব দেখায়। Windows 10/11-এর 64-bit কম্পিউটারের জন্য।

## চালু করুন

1. [সর্বশেষ সংস্করণ](https://github.com/almuzahidseyam/TrafficSense/releases/latest) থেকে `TrafficSense-Setup.exe` অথবা `TrafficSense.zip` নামান।
2. Setup দিয়ে ইনস্টল করুন, অথবা ZIP সম্পূর্ণ extract করে `TrafficSense.exe` খুলুন। আলাদা .NET ইনস্টল লাগবে না।
3. উইজেটে right-click → **Preferences → Language → বাংলা** নির্বাচন করুন।
4. **সংযোগ ও গতি → নেটওয়ার্ক অ্যাডাপ্টার** থেকে Auto রাখুন, অথবা নির্দিষ্ট Wi-Fi/Ethernet/VPN বাছুন।

নতুন সংস্করণ চালুর আগে আগেরটির tray মেনু থেকে **প্রস্থান** করুন এবং Sensor bridge বন্ধ করুন। শুধু লুকালে অ্যাপ চালু থাকে; আবার খুললে আগের চলমান কপিটিই সামনে আসে।

## প্রয়োজনীয় সেটিং

- **গতির একক:** MB/s ও Mbps এক নয়। bytes-এর প্রদর্শন binary, bits-এর প্রদর্শন decimal।
- **গতি আপডেটের ব্যবধান:** ১ সেকেন্ড ডিফল্ট। ০.২৫ সেকেন্ড দ্রুত বদলায়, তবে CPU বেশি লাগতে পারে এবং driver দেরিতে counter দিলে গতি ওঠানামা করতে পারে।
- **অবস্থান লক/ফিরিয়ে আনা:** ভুলে টেনে সরানো বন্ধ করুন অথবা মূল মনিটরের দৃশ্যমান স্থানে ফিরুন।
- **লেখার আকার/ফন্ট/রং/স্বচ্ছতা:** সঙ্গে সঙ্গে কার্যকর হয়। Skin JSON দিয়ে শুধু চেহারার সেটিং আদানপ্রদান করা যায়।
- **System metrics:** দরকারমতো CPU, RAM, frequency, GPU ও disk চালু করুন। বন্ধ থাকলে ঐচ্ছিক counter সংগ্রহ বন্ধ হয়।
- **Taskbar-এ দেখানো:** উপরে/নিচে edge panel, অথবা পরীক্ষামূলকভাবে taskbar-এর ভেতরে। খালি জায়গা যাচাই না হলে edge panel দেখাবে। নির্বাচিত monitor-এর সেটিং এখানে পাওয়া যাবে।
- **TrafficMonitor skin:** Widget-এর চেহারা ও অবস্থান থেকে INI/XML skin আনুন। ছোট/বড় layout, PNG/BMP background ও label পড়বে। [নিজস্ব Mint উদাহরণ](../examples/skins/Mint) ও [skin নির্দেশনা](SKINS.md) দেখুন।
- **Native plugin bridge:** সিস্টেমের তথ্য ও extension → Sensor source থেকে খুলুন। শুধুমাত্র বিশ্বস্ত x64 DLL বাছুন; আলাদা worker-এ text, drawing, settings ও command চলে। এটি security sandbox নয়। Stop বা bridge বন্ধ করলে worker থামে। [সমর্থন ও সীমা](NATIVE-PLUGINS.md)।

## হিসাব দেখুন বা কপি করুন

**ডেটা ব্যবহার ও ইতিহাস** খুলে মাস/তারিখের সীমা বাছুন। মোট download/upload, দিনভিত্তিক তালিকা ও chart দেখুন। Chart-এর দিনে ক্লিক করলে সংশ্লিষ্ট সারি নির্বাচিত হয়। দিন নির্বাচন করে সাধারণ লেখা অথবা CSV ফরম্যাটে কপি করুন; CSV export-এ exact byte থাকে। Clipboard ব্যস্ত থাকলে আবার চেষ্টা করুন।

দৈনিক/মাসিক সতর্কতার GiB সীমা দিলে চলমান পর্যবেক্ষণে সীমা পার হওয়ার সময় notification দেখানোর চেষ্টা করবে। Windows notification বন্ধ রাখলে দেখা নাও যেতে পারে। আগেই পার হওয়া হিসাব নিয়ে বারবার সতর্ক করবে না।

এই হিসাব অ্যাপ চলার সময় পর্যবেক্ষিত traffic-এর। Sleep/reconnect বা অ্যাপ বন্ধ থাকা সময়ের অজানা traffic যোগ করা হয় না। এটি ISP-এর বিলের হিসাব বা Internet speed test নয়। সংযোগের নমুনা না থাকলে status দেখাবে, chart-এ ফাঁকা থাকবে।

## Temperature ও সহায়তা

Temperature চাইলে আগে উপযুক্ত [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) চালু করে তার web server সক্রিয় করুন। TrafficSense-এর Sensor extension থেকে Sensor bridge খুলে স্থানীয় port দিয়ে Connect করুন। Driver/সেন্সর সমর্থন provider-এর দায়িত্ব; TrafficSense driver ইনস্টল করে না। [বিস্তারিত](EXTENSIONS.md)।

**About** থেকে version, update check ও log folder পাওয়া যায়। সমস্যা জানাতে [bug-report ফর্ম](https://github.com/almuzahidseyam/TrafficSense/issues/new/choose) ব্যবহার করুন। Version, Windows build, adapter/VPN ও কীভাবে সমস্যা হচ্ছে লিখুন; শেয়ার করার আগে log-এর ব্যক্তিগত তথ্য দেখুন।

[ফিচার ও সীমাবদ্ধতা](COMPATIBILITY.md) · [গতি মাপার পদ্ধতি](MEASUREMENT.md) · [মূল README](../README.md)

v1.28-এ dark plugin preview, স্বাভাবিক বন্ধের সময় plugin settings সংরক্ষণ এবং monitor অনুযায়ী DPI scaling সংশোধন হয়েছে। চার community plugin ও সক্রিয় WARP-এর নির্দিষ্ট পরীক্ষা পাস করেছে। বাস্তব দ্বিতীয় monitor/mixed-DPI ও VPN reconnect এখনো পরীক্ষা হয়নি। [পরীক্ষার ফল ও পুনরায় চালানোর নির্দেশনা](COMPATIBILITY-TESTING.md)।
