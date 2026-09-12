using DNSYar.Models;

namespace DNSYar.Services;

public sealed class TrayIconService : IDisposable
{
    public event Action? ShowRequested;
    public event Action? RunSmartNowRequested;
    public event Action? RestoreRequested;
    public event Action? ExitRequested;
    public event Action<bool>? SmartAutoChanged;
    public event Action<DnsProvider>? ProviderRequested;

    public bool IsInitialized => false;

    public void Initialize(bool smartEnabled) { }
    public void SetVisible(bool visible) { }
    public void Update(string activeDnsName, bool smartEnabled, IEnumerable<DnsProvider> providers) { }
    public void Notify(string title, string message, int icon = 0) { }
    public void Dispose() { }
}
