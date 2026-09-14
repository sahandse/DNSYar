using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DNSYar.Services;

namespace DNSYar;

public partial class App : Application
{
    private const string SingleInstanceName = @"Local\Sahandse.DNSYar.SingleInstance";
    private Window? _window;
    private Mutex? _singleInstance;

    public App()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Report(ex);
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _singleInstance = new Mutex(true, SingleInstanceName, out var createdNew);
        if (!createdNew)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Environment.Exit(0);
            return;
        }

        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DNSYar");
            var logPath = Path.Combine(dir, "launch-error.txt");
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(logPath, $"[{DateTime.Now:O}]\r\n{ex}\r\n");
            }
            catch { }

            _window = new Window
            {
                Title = "DNSYar — Startup Error",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Padding = new Thickness(24),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = "DNSYar — Startup Error", FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.Bold },
                        new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap, MaxWidth = 560 },
                        new TextBlock { Text = $"Log: {logPath}", FontSize = 11, Opacity = 0.6 }
                    }
                }
            };
            _window.Activate();
        }
    }
}
