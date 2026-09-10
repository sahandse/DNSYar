using Microsoft.UI.Xaml;
using DNSYar.Services;

namespace DNSYar;

public partial class App : Application
{
    private const string SingleInstanceName = @"Local\Sahandse.DNSYar.SingleInstance";
    private Window? _window;
    private Mutex? _singleInstance;

    public App()
    {
        InitializeComponent();
        Resources["T"] = UiText.Current;
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

        _window = new MainWindow();
        _window.Activate();
    }
}
