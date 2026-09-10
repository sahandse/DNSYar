# DNSYar — Smart DNS Manager فارسی برای ویندوز

DNSYar یک برنامه WinUI 3 برای Windows 10/11 است که DNSها را تست، رتبه‌بندی و با یک کلیک اعمال می‌کند. رتبه‌بندی فقط Ping نیست و DNS Query، Packet Loss و دسترسی واقعی سرویس‌های AI، Developer، Gaming و سایت دلخواه بررسی می‌شود.

## سازنده
- **سهند رضوان**
- تلگرام: `t.me/sahandse`

## تکنولوژی
- C# / .NET 8
- WinUI 3
- Microsoft Windows App SDK 2.3.1
- Unpackaged desktop app

## قابلیت‌های نسخه 0.7.3
- UI مینیمال دوزبانه (فارسی RTL و English LTR) با سوییچ زبان
- فونت Vazirmatn و Inter همراه برنامه
- تم‌های Paper / Ink / Signal
- داشبورد Hero با کارت‌های آماری فشرده و گرافیک شبکه‌ای
- چیدمان Responsive: کارت‌های آماری/تم/نتیجه سایت Wrap می‌شوند و بخش‌های اصلی در عرض کم به حالت ستونی می‌روند
- جستجوی زنده DNS بر اساس نام، IP و منبع
- انتخاب کارت شبکه و نمایش DNS فعال
- تست Ping، Packet Loss و DNS Query
- تست واقعی AI و برنامه‌نویسی
- تست گسترده سرویس‌های بازی
- تست یک سایت دلخواه با تمام DNSها و نمایش سبز/قرمز
- امتیازدهی و پیشنهاد بهترین DNS
- اعمال DNS با دسترسی Administrator
- بازگردانی دقیق DNS قبلی هنگام خروج / Shutdown / Recovery
- بروزرسانی خودکار و دستی DNS از GitHub
- Merge هوشمند DNSهای آنلاین با لیست محلی
- افزودن DNS دستی و نگهداری مستقل در `custom-dns.json`
- Smart Auto DNS + Failover با پروفایل‌های AI / Developer / Gaming / All
- پایش دوره‌ای ۵ تا ۶۰ دقیقه و Failover بعد از افت متوالی قابل تنظیم
- System Tray واقعی با Quick Switch، تست هوشمند، Restore و Exit امن
- گزینه اختیاری Close-to-Tray؛ پیش‌فرض خاموش برای حفظ Restore-on-Exit


## Smart Auto DNS + Failover
Smart Auto DNS به‌صورت پیش‌فرض خاموش است و کاربر آن را از Settings فعال می‌کند. منطق آن برای جلوگیری از سوییچ‌های بی‌دلیل دو مرحله‌ای است:

1. ابتدا فقط DNS فعال با Endpointهای سبک همان پروفایل Health Check می‌شود.
2. اگر DNS فعال به حداقل Score نرسد، شمارنده افت افزایش می‌یابد.
3. بعد از تعداد افت متوالی انتخاب‌شده (۱، ۲ یا ۳)، DNSهای مناسب همان پروفایل به‌صورت موازی Benchmark می‌شوند.
4. فقط DNSی که حداقل Score و حداقل Reachability را داشته باشد انتخاب می‌شود.
5. اگر هیچ جایگزین سالمی پیدا نشود، DNS فعلی دست‌نخورده می‌ماند.

پروفایل‌ها: `all`، `ai`، `dev` و `game`. فاصله بررسی: ۵، ۱۰، ۱۵، ۳۰ یا ۶۰ دقیقه.

## System Tray + Quick Switch
اگر Tray فعال باشد، کنار ساعت Windows یک منوی سریع وجود دارد:

- نمایش DNS فعال
- باز کردن DNSYar
- Quick Switch تا ۱۰ DNS برتر
- روشن/خاموش کردن Smart Auto DNS
- اجرای Smart Check فوری
- بازگردانی DNS قبلی
- خروج کامل

گزینه «× به Tray» **پیش‌فرض خاموش** است. در حالت پیش‌فرض، بستن پنجره همان خروج کامل است و اگر Restore-on-Exit روشن باشد DNS قبلی بازیابی می‌شود. اگر کاربر Close-to-Tray را روشن کند، × فقط پنجره را مخفی می‌کند؛ خروج کامل از منوی Tray همچنان مسیر Restore امن را اجرا می‌کند.

## سرویس‌های بازی نسخه 0.7.0
بخش «بازی» اکنون **۱۹ گروه/پلتفرم** و **۴۲ Endpoint** را بررسی می‌کند:

- Epic Games
- PlayStation
- Steam
- Xbox
- Riot / Valorant / League of Legends
- Battle.net / Blizzard
- EA / Apex
- Ubisoft Connect
- Rockstar / GTA
- Nintendo
- GOG
- Roblox
- Minecraft
- Call of Duty
- PUBG
- Twitch
- NVIDIA GeForce NOW
- Discord
- FACEIT

هر گروه چند Endpoint دارد. کارت هر DNS نتیجه را به شکل `🟢 Steam (3/3)` یا `🔴 Xbox (0/3)` نمایش می‌دهد. در حالت Gaming، امتیازدهی نسبت به حالت‌های دیگر وزن بیشتری به Ping و Packet Loss می‌دهد.

## تست سایت با همه DNSها
در منوی «تست سایت» دامنه‌ای مثل `google.com` یا `https://chatgpt.com` را وارد کنید. DNSYar بدون تغییر DNS فعلی ویندوز:

1. دامنه را مستقیماً با هر DNS Resolve می‌کند.
2. با IP برگشتی اتصال واقعی HTTP/HTTPS می‌سازد.
3. زمان DNS و HTTP را ثبت می‌کند.
4. نتیجه موفق را سبز و ناموفق را قرمز نمایش می‌دهد.
5. سریع‌ترین DNS موفق را مشخص می‌کند.

## بروزرسانی خودکار از GitHub
منبع پیش‌فرض عمومی:

`https://raw.githubusercontent.com/mehrshadasgary/Iran-DNS-Switcher/main/dns_servers.txt`

این آدرس در Settings قابل تغییر است. برنامه JSON و TXT را می‌خواند و لینک `github.com/.../blob/...` را به Raw تبدیل می‌کند.

رفتار بروزرسانی:
- فقط IPv4 معتبر پذیرفته می‌شود.
- DNS جدید اضافه می‌شود و مورد موجود Merge می‌شود.
- DNSی که از منبع حذف شده خودکار از سیستم کاربر پاک نمی‌شود.
- DNS دستی کاربر هرگز توسط GitHub حذف نمی‌شود.
- دریافت لیست هیچ DNSی را خودکار روی Windows فعال نمی‌کند.

## افزودن DNS دستی
فیلدها:
- نام
- Primary IPv4
- Secondary IPv4 اختیاری
- DoH URL اختیاری
- دسته‌بندی: همه / AI / Developer / Game

ذخیره محلی:

`%LocalAppData%\DNSYar\custom-dns.json`

## بازگردانی امن DNS
قبل از اولین تغییر DNS هر کارت شبکه، وضعیت واقعی در Snapshot محلی ذخیره می‌شود:
- اگر قبل از DNSYar روی DHCP بوده، DHCP برمی‌گردد.
- اگر DNS دستی داشته، همان DNSهای قبلی برمی‌گردند.
- Snapshot هر کارت شبکه مستقل است.

در Settings دو گزینه وجود دارد:
1. بازگردانی DNS قبلی هنگام بستن / Shutdown / Restart
2. Recovery در اجرای بعدی اگر Process ناگهانی متوقف شده باشد

## تم‌ها
- **Paper** — روشن و گرم
- **Ink** — تیره ذغالی
- **Signal** — تیره با لهجه تیل

## پیش‌نیاز Build
1. Windows 10 19041+ یا Windows 11
2. Visual Studio با workload توسعه Desktop/WinUI و Windows SDK
3. .NET 8 SDK
4. NuGet Restore

فایل `DNSYar.csproj` را باز کنید و Build/Run بزنید. برنامه برای تغییر DNS با `requireAdministrator` اجرا می‌شود.

## نکته Build این بسته
در محیط تولید این ZIP ابزار Windows SDK/WinUI موجود نبود، بنابراین XML/XAML، JSON، نام Event Handlerها و ساختار پروژه به‌صورت استاتیک اعتبارسنجی شده‌اند، اما خروجی EXE اینجا Build نشده است.


## ساخت سریع EXE
- روی Windows فایل `Build-EXE.bat` را اجرا کنید.
- خروجی در `Release\win-x64` ساخته می‌شود.
- راهنمای کامل در `INSTALL.md` است.
- GitHub Actions نیز در `.github/workflows/build-windows.yml` آماده است.
