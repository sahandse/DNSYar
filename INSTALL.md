# نصب و ساخت DNSYar روی ویندوز

## روش پیشنهادی: ساخت EXE با یک کلیک

### پیش‌نیازها
- Windows 10 (19041+) یا Windows 11
- Visual Studio 2022
- در Visual Studio Installer، workload مربوط به **.NET desktop development** و اجزای **Windows App SDK / WinUI** را نصب کنید.
- .NET 8 SDK
- Windows SDK سازگار با Target پروژه (پروژه فعلی `10.0.26100.0` را هدف گرفته است.)

### ساخت
1. ZIP پروژه را Extract کنید.
2. وارد پوشه `DNSYar-WinUI3` شوید.
3. روی `Build-EXE.bat` دوبار کلیک کنید.
4. بعد از موفقیت، پوشه زیر باز می‌شود:

   `Release\win-x64`

5. فایل اصلی برنامه:

   `DNSYar.exe`

> DNSYar برای تغییر DNS نیاز به Administrator دارد و ویندوز هنگام اجرا UAC نمایش می‌دهد.

## روش Visual Studio
1. `DNSYar.csproj` را با Visual Studio باز کنید.
2. NuGet Restore را انجام دهید.
3. Configuration را روی `Release` و Platform را روی `x64` قرار دهید.
4. Build کنید.
5. برای خروجی قابل جابه‌جایی بهتر، از Publish با Runtime `win-x64` و حالت Self-contained استفاده کنید.

## GitHub Actions
فایل `.github/workflows/build-windows.yml` همراه پروژه است. اگر پروژه را در GitHub Push کنید، از تب **Actions** می‌توانید workflow به نام `Build DNSYar Windows` را دستی اجرا کنید. خروجی `DNSYar-win-x64` به‌صورت Artifact قابل دریافت خواهد بود.

## نصب روی سیستم دیگر
خروجی این پروژه Folder-based است؛ پوشه `Release\win-x64` را کامل روی سیستم مقصد کپی کنید و `DNSYar.exe` را اجرا کنید. فقط خود EXE را جدا نکنید، چون WinUI و فایل‌های وابسته ممکن است کنار آن لازم باشند.

## حذف برنامه
چون این نسخه Unpackaged است، Uninstall کلاسیک ندارد. کافی است:
1. از DNSYar خروج کامل بزنید تا DNS قبلی بازیابی شود.
2. پوشه برنامه را حذف کنید.
3. در صورت تمایل داده‌های محلی را از `%LocalAppData%\DNSYar` پاک کنید.
