using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace DNSYar;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            ReportFatal(ex, "OnLaunched");
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        ReportFatal(e.Exception, "UnhandledException");
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) ReportFatal(ex, "AppDomain");
    }

    // No XAML window may exist yet (or may already be gone) when this runs, so this can't
    // rely on ContentDialog/InfoBar - a plain Win32 message box always works, and the full
    // exception is also written to disk so a crash that flashes past can still be diagnosed.
    private static void ReportFatal(Exception ex, string source)
    {
        string? logPath = null;
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DNSYar");
            Directory.CreateDirectory(root);
            logPath = Path.Combine(root, "crash.log");
            File.AppendAllText(logPath, $"[{DateTimeOffset.Now:O}] ({source})\n{ex}\n\n");
        }
        catch { }

        try
        {
            var locationHint = logPath is null ? "" : $"\n\nجزئیات ذخیره شد در:\n{logPath}";
            MessageBoxW(0, $"DNSYar با خطای غیرمنتظره‌ای مواجه شد:\n\n{ex.Message}{locationHint}", "DNSYar - خطا", 0x10 /* MB_ICONERROR */);
        }
        catch { }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);
}
