using System.ComponentModel;
using System.Runtime.CompilerServices;
using DNSYar.Services;
using Windows.UI;
using Microsoft.UI.Xaml.Media;

namespace DNSYar.Models;

public sealed class SiteDnsTestResult : INotifyPropertyChanged
{
    private string _statusText = "در انتظار تست";
    private string _dnsText = "DNS: —";
    private string _httpText = "HTTP: —";
    private string _details = "هنوز تست نشده";
    private string _resolvedText = "IP: —";
    private SolidColorBrush _statusBrush = new(new Color { A = 255, R = 128, G = 128, B = 128 });
    private SolidColorBrush _cardBrush = new(new Color { A = 42, R = 100, G = 116, B = 139 });
    private bool _isTesting;
    private bool _isSuccess;
    private double? _httpMs;
    private bool _isCompleted;

    public SiteDnsTestResult(DnsProvider provider) => Provider = provider;

    public DnsProvider Provider { get; }
    public string ProviderName => Provider.Name;
    public string Addresses => Provider.Addresses;
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public string DnsText { get => _dnsText; private set => Set(ref _dnsText, value); }
    public string HttpText { get => _httpText; private set => Set(ref _httpText, value); }
    public string Details { get => _details; private set => Set(ref _details, value); }
    public string ResolvedText { get => _resolvedText; private set => Set(ref _resolvedText, value); }
    public SolidColorBrush StatusBrush { get => _statusBrush; private set => Set(ref _statusBrush, value); }
    public SolidColorBrush CardBrush { get => _cardBrush; private set => Set(ref _cardBrush, value); }
    public bool IsTesting { get => _isTesting; private set => Set(ref _isTesting, value); }
    public bool IsSuccess { get => _isSuccess; private set => Set(ref _isSuccess, value); }
    public double? HttpMs { get => _httpMs; private set => Set(ref _httpMs, value); }
    public bool IsCompleted { get => _isCompleted; private set => Set(ref _isCompleted, value); }

    public void Start()
    {
        IsCompleted = false;
        IsTesting = true;
        IsSuccess = false;
        StatusText = "در حال تست…";
        DnsText = "DNS: …";
        HttpText = "HTTP: …";
        Details = "در حال Resolve و بررسی اتصال واقعی سایت";
        ResolvedText = "IP: …";
        StatusBrush = new SolidColorBrush(new Color { A = 255, R = 100, G = 116, B = 139 });
        CardBrush = new SolidColorBrush(new Color { A = 70, R = 100, G = 116, B = 139 });
    }

    public void Complete(SiteProbeResult result)
    {
        IsTesting = false;
        IsCompleted = true;
        IsSuccess = result.Reachable;
        HttpMs = result.HttpElapsedMs;
        DnsText = result.DnsResolved ? $"DNS: {result.DnsElapsedMs:0} ms" : "DNS: بدون پاسخ";
        HttpText = result.Reachable
            ? $"HTTP: {result.HttpElapsedMs:0} ms • {result.StatusCode}"
            : result.StatusCode is > 0 ? $"HTTP: خطا • {result.StatusCode}" : "HTTP: ناموفق";
        ResolvedText = result.Addresses.Count == 0 ? "IP: —" : $"IP: {string.Join(" • ", result.Addresses.Take(3))}";

        if (result.Reachable)
        {
            StatusText = "باز می‌شود";
            Details = result.FinalUri is null ? "اتصال واقعی سایت با این DNS موفق بود" : $"موفق • {result.FinalUri.Host}";
            StatusBrush = new SolidColorBrush(new Color { A = 255, R = 22, G = 163, B = 74 });
            CardBrush = new SolidColorBrush(new Color { A = 95, R = 22, G = 163, B = 74 });
        }
        else
        {
            StatusText = "باز نمی‌شود";
            Details = result.DnsResolved
                ? (string.IsNullOrWhiteSpace(result.Error) ? "DNS پاسخ داد، اما اتصال سایت ناموفق بود" : result.Error)
                : "این DNS دامنه را Resolve نکرد";
            StatusBrush = new SolidColorBrush(new Color { A = 255, R = 220, G = 38, B = 38 });
            CardBrush = new SolidColorBrush(new Color { A = 95, R = 220, G = 38, B = 38 });
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
