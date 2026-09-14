using System.ComponentModel;
using System.Runtime.CompilerServices;
using DNSYar.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DNSYar.Models;

public sealed class SiteDnsTestResult : INotifyPropertyChanged
{
    private string _statusText = "";
    private string _dnsText = "";
    private string _httpText = "";
    private string _details = "";
    private string _resolvedText = "";
    private SolidColorBrush _statusBrush = new(ColorHelper.FromArgb(255, 148, 163, 184));
    private SolidColorBrush _cardBrush = new(ColorHelper.FromArgb(42, 100, 116, 139));
    private bool _isTesting;
    private bool _isSuccess;
    private double? _httpMs;
    private bool _isCompleted;

    public SiteDnsTestResult(DnsProvider provider)
    {
        Provider = provider;
        StatusText = UiText.Current.G("SiteWaiting");
        DnsText = UiText.Current.G("DnsPending");
        HttpText = UiText.Current.G("HttpPending");
        Details = UiText.Current.G("NotTested");
        ResolvedText = UiText.Current.G("IpNone");
    }

    public DnsProvider Provider { get; }
    public string ProviderName => Provider.Name;
    public string Addresses => Provider.Addresses;
    public string ConnectLabel => UiText.Current.Connect;
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
        StatusText = UiText.Current.G("SiteTesting");
        DnsText = UiText.Current.G("DnsEllipsis");
        HttpText = UiText.Current.G("HttpEllipsis");
        Details = UiText.Current.G("SiteTestingDetail");
        ResolvedText = UiText.Current.G("IpEllipsis");
        StatusBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 100, 116, 139));
        CardBrush = new SolidColorBrush(ColorHelper.FromArgb(70, 100, 116, 139));
    }

    public void Complete(SiteProbeResult result)
    {
        IsTesting = false;
        IsCompleted = true;
        IsSuccess = result.Reachable;
        HttpMs = result.HttpElapsedMs;
        DnsText = result.DnsResolved ? UiText.Current.F("DnsMsFmt", result.DnsElapsedMs) : UiText.Current.G("DnsNoReply");
        HttpText = result.Reachable
            ? UiText.Current.F("HttpOkFmt", result.HttpElapsedMs, result.StatusCode)
            : result.StatusCode is > 0 ? UiText.Current.F("HttpErrFmt", result.StatusCode) : UiText.Current.G("HttpFail");
        ResolvedText = result.Addresses.Count == 0
            ? UiText.Current.G("IpNone")
            : UiText.Current.F("IpFmt", string.Join(" • ", result.Addresses.Take(3)));

        if (result.Reachable)
        {
            StatusText = UiText.Current.G("SiteOpen");
            Details = result.FinalUri is null ? UiText.Current.G("SiteOk") : UiText.Current.F("SiteOkHost", result.FinalUri.Host);
            StatusBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 22, 163, 74));
            CardBrush = new SolidColorBrush(ColorHelper.FromArgb(95, 22, 163, 74));
        }
        else
        {
            StatusText = UiText.Current.G("SiteClosed");
            Details = result.DnsResolved
                ? (string.IsNullOrWhiteSpace(result.Error) ? UiText.Current.G("SiteHttpFail") : result.Error)
                : UiText.Current.G("SiteDnsFail");
            StatusBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 220, 38, 38));
            CardBrush = new SolidColorBrush(ColorHelper.FromArgb(95, 220, 38, 38));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyLanguage() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConnectLabel)));

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
