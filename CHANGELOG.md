## 0.7.2
- Fixed WinUI build: moved System Tray (WinForms NotifyIcon) into `DNSYar.Tray` so `UseWinUI` and `UseWindowsForms` are no longer mixed in one project (MC6000).
- Fixed `FontFamily` on `Grid` (WinUI 3 has no such property) by hosting the font on a `ContentControl`.
- Fixed `ColorHelper` namespace (`Microsoft.UI`) so the app compiles against Windows App SDK.
- Defined `AccentButtonStyle` in `App.xaml` to prevent XamlParseException on launch.
- Prevented two DNSYar instances from fighting over DNS with a single-instance mutex.
- Serialized DNS apply/restore so a concurrent switch cannot snapshot the wrong servers.
- Catalog updates now require HTTPS; the update URL is persisted on focus-loss instead of every keystroke.
- DNS responses must match the query ID and be in-bounds before A records are accepted.

## 0.7.1
- Added one-click Windows x64 build scripts (`Build-EXE.bat`, `Build-EXE.ps1`).
- Added INSTALL.md.
- Added GitHub Actions Windows release build workflow.

# DNSYar Changelog

## 0.7.0
- اضافه‌شدن Smart Auto DNS با پروفایل‌های همه‌کاره، AI، Developer و Gaming.
- Health Check سبک روی DNS فعال؛ Benchmark کامل فقط هنگام نیاز به Failover یا اجرای دستی.
- Failover محافظه‌کارانه با حداقل Score قابل تنظیم و ۱/۲/۳ افت متوالی قبل از سوییچ.
- انتخاب خودکار بهترین DNS سالم بر اساس Reachability، DNS latency، Ping و Packet Loss.
- نمایش وضعیت Smart Auto در داشبورد و تنظیمات، به‌همراه دکمه «Smart Auto الآن».
- System Tray واقعی برای Windows با نمایش DNS فعال.
- Quick Switch تا ۱۰ DNS برتر از منوی Tray.
- کنترل Smart Auto، تست فوری، Restore DNS و Exit کامل از Tray.
- گزینه اختیاری «× به Tray»؛ پیش‌فرض خاموش تا Restore-on-Exit قبلی حفظ شود.
- هنگام Shutdown/Logoff، حالت Close-to-Tray نادیده گرفته می‌شود و Restore امن همچنان اجرا می‌شود.

## 0.6.0
- بازطراحی کامل UI با داشبورد Hero، کارت‌های مدرن، فاصله‌گذاری و تایپوگرافی جدید.
- چیدمان Wrap شونده برای کارت‌های آماری، پوشش سرویس‌ها، نتیجه تست سایت و انتخاب تم.
- اضافه‌شدن جستجوی زنده DNS بر اساس نام، Primary/Secondary و منبع.
- کارت DNS جدید با امتیاز رنگی، وضعیت، Ping، DNS latency، Packet Loss و Services.
- گسترش تست بازی به پلتفرم‌های بیشتر: Epic Games، PlayStation، Steam، Xbox، Riot/Valorant/LoL، Battle.net، EA/Apex، Ubisoft Connect، Rockstar/GTA، Nintendo، GOG، Roblox، Minecraft، Call of Duty، PUBG، Twitch، GeForce NOW، Discord و FACEIT.
- چند Endpoint برای هر پلتفرم و نمایش سبز/قرمز + تعداد Endpointهای موفق.
- امتیازدهی اختصاصی Game با وزن بیشتر برای Ping و Packet Loss.
- نمایش تعداد پلتفرم‌ها و Endpointهای بازی در داشبورد و هدر بخش بازی.


## 0.5.0
- بخش جدید «بازی» برای تست DNS روی Epic Games، PlayStation، Steam و Xbox.
- تست چند Endpoint برای هر سرویس بازی و نمایش نتیجه سبز/قرمز به تفکیک هر DNS.
- نمایش تعداد Endpointهای موفق کنار هر سرویس، مثل Steam (2/2).
- امتیازدهی و پیشنهاد اختصاصی DNS برای سرویس‌های بازی.
- امکان افزودن DNS دستی مستقیماً به دسته «بازی».
- اضافه‌شدن وضعیت جزئی سرویس‌ها به کارت هر DNS.

## 0.4.0
- بروزرسانی خودکار و دستی لیست DNS از GitHub با منبع پیش‌فرض عمومی.
- پشتیبانی از JSON و فرمت متنی `dns_servers.txt`.
- تبدیل خودکار لینک GitHub `blob` به Raw برای دریافت فایل.
- Merge هوشمند DNSهای آنلاین؛ موارد قبلی و DNSهای دستی حذف نمی‌شوند.
- افزودن DNS دستی با نام، Primary، Secondary، DoH و دسته‌بندی.
- ذخیره DNSهای دستی در `custom-dns.json` مستقل از کاتالوگ آنلاین.
- انتخاب دوره بروزرسانی ۶، ۱۲، ۲۴ یا ۷۲ ساعت.
- بررسی دوره‌ای بروزرسانی در زمان اجرای برنامه.
- نمایش منبع هر DNS در کارت آن.

## 0.3.0
- افزودن بخش «تست سایت» برای بررسی یک آدرس با تمام DNSها به‌صورت هم‌زمان.
- نمایش زنده نتیجه سبز/قرمز برای هر DNS بدون تغییر DNS فعلی ویندوز.
- Resolve مستقیم دامنه توسط هر DNS و سپس اتصال واقعی HTTP/HTTPS با همان Resolver.
- نمایش زمان DNS، زمان HTTP، Status Code و IPهای Resolve شده.
- مرتب‌سازی DNSهای موفق در بالای نتایج و نمایش سریع‌ترین DNS موفق.
- امکان اتصال مستقیم به هر DNS از همان نتیجه تست سایت.

## 0.2.0
- ذخیره Snapshot تنظیم DNS قبل از اولین تغییر.
- بازگردانی دقیق DNS قبلی به جای DHCP اجباری.
- گزینه Restore on Exit / Shutdown در تنظیمات.
- گزینه Recovery بعد از Crash یا قطع ناگهانی.
- پشتیبانی از SessionEnding ویندوز.
- دکمه بازگردانی همه کارت‌های تغییر داده‌شده.
- پنج تم Glass 3D، Aurora، Cyber Neon، Ocean Depth و Graphite 3D.
