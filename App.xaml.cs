using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DNSYar;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
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
            var fullError = ex.ToString();
            var logPath = Path.Combine(AppContext.BaseDirectory, "crash.log");
            try { File.WriteAllText(logPath, $"[{DateTime.Now}]\r\n{fullError}\r\n"); } catch { }

            try
            {
                _window = new Window();
                _window.Content = new Grid
                {
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 12,
                            Padding = new Thickness(24),
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Children =
                            {
                                new TextBlock { Text = "DNSYar — Startup Error", FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.Bold },
                                new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap, MaxWidth = 500 },
                                new TextBlock { Text = $"Log saved to: {logPath}", FontSize = 11, Opacity = 0.5 }
                            }
                        }
                    }
                };
                _window.Title = "DNSYar Error";
                _window.Activate();
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments = $"\"{logPath}\"",
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }
    }
}
