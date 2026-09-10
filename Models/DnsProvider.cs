using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using DNSYar.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DNSYar.Models;

public sealed class DnsProvider : INotifyPropertyChanged
{
    private double? _pingMs;
    private double? _dnsMs;
    private double _packetLoss;
    private int _score;
    private int _reachableServices;
    private int _totalServices;
    private bool _isActive;
    private string _status = "";
    private string _recommendation = "";
    private string _serviceDetailsText = "";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "DNS";
    public string Primary { get; set; } = "";
    public string? Secondary { get; set; }
    public string? DoH { get; set; }
    public string[] Categories { get; set; } = Array.Empty<string>();
    public string Description { get; set; } = "";
    public bool IsCustom { get; set; }
    public string Source { get; set; } = "داخلی";

    [JsonIgnore]
    public Dictionary<string, bool> ServiceResults { get; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public double? PingMs { get => _pingMs; set { _pingMs = value; OnChanged(); OnChanged(nameof(PingText)); } }
    [JsonIgnore]
    public double? DnsMs { get => _dnsMs; set { _dnsMs = value; OnChanged(); OnChanged(nameof(DnsText)); } }
    [JsonIgnore]
    public double PacketLoss { get => _packetLoss; set { _packetLoss = value; OnChanged(); OnChanged(nameof(LossText)); } }
    [JsonIgnore]
    public int Score { get => _score; set { _score = value; OnChanged(); OnChanged(nameof(ScoreText)); OnChanged(nameof(ScoreBrush)); } }
    [JsonIgnore]
    public int ReachableServices { get => _reachableServices; set { _reachableServices = value; OnChanged(); OnChanged(nameof(ServiceText)); OnChanged(nameof(ServiceBrush)); } }
    [JsonIgnore]
    public int TotalServices { get => _totalServices; set { _totalServices = value; OnChanged(); OnChanged(nameof(ServiceText)); OnChanged(nameof(ServiceBrush)); } }
    [JsonIgnore]
    public bool IsActive { get => _isActive; set { _isActive = value; OnChanged(); OnChanged(nameof(ActiveText)); OnChanged(nameof(ActiveBrush)); } }
    [JsonIgnore]
    public string Status { get => _status; set { _status = value; OnChanged(); OnChanged(nameof(StatusBrush)); } }
    [JsonIgnore]
    public string Recommendation { get => _recommendation; set { _recommendation = value; OnChanged(); } }
    [JsonIgnore]
    public string ServiceDetailsText { get => _serviceDetailsText; set { _serviceDetailsText = value; OnChanged(); } }

    [JsonIgnore] public string PingText => PingMs is null ? "—" : $"{PingMs:0} ms";
    [JsonIgnore] public string DnsText => DnsMs is null ? "—" : $"{DnsMs:0} ms";
    [JsonIgnore] public string LossText => $"{PacketLoss:0}%";
    [JsonIgnore] public string ScoreText => Score == 0 ? "—" : $"{Score}/100";
    [JsonIgnore] public string ServiceText => TotalServices == 0 ? "—" : $"{ReachableServices}/{TotalServices}";
    [JsonIgnore] public string ActiveText => IsActive ? UiText.Current.Active : "";
    [JsonIgnore] public string Addresses => string.IsNullOrWhiteSpace(Secondary) ? Primary : $"{Primary}  •  {Secondary}";
    [JsonIgnore] public string SourceText => IsCustom ? UiText.Current.CustomSource : Source;
    [JsonIgnore] public Brush ScoreBrush => BrushForScore(Score);
    [JsonIgnore] public Brush StatusBrush => IsErrorStatus ? Solid("#EF4444") : BrushForScore(Score);
    [JsonIgnore] public Brush ActiveBrush => IsActive ? Solid("#22C55E") : Solid("#94A3B8");
    [JsonIgnore] public Brush ServiceBrush
    {
        get
        {
            if (TotalServices == 0) return Solid("#94A3B8");
            var rate = ReachableServices / (double)TotalServices;
            return rate >= .85 ? Solid("#22C55E") : rate >= .55 ? Solid("#F59E0B") : Solid("#EF4444");
        }
    }

    private static Brush BrushForScore(int score) =>
        score >= 85 ? Solid("#22C55E") : score >= 65 ? Solid("#06B6D4") : score >= 40 ? Solid("#F59E0B") : Solid("#94A3B8");

    private static SolidColorBrush Solid(string hex)
    {
        var raw = hex.TrimStart('#');
        return new SolidColorBrush(ColorHelper.FromArgb(255,
            Convert.ToByte(raw.Substring(0, 2), 16),
            Convert.ToByte(raw.Substring(2, 2), 16),
            Convert.ToByte(raw.Substring(4, 2), 16)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void NotifyLanguage()
    {
        if (PingMs is null && Score == 0)
        {
            Status = UiText.Current.ReadyToTest;
            Recommendation = UiText.Current.G("NotTested");
            ServiceDetailsText = UiText.Current.G("ServiceDetailsHint");
        }
        if (!IsCustom && (Source is "داخلی" or "Built-in" or "Internal" or ""))
            Source = UiText.Current.G("InternalSource");
        OnChanged(nameof(ActiveText));
        OnChanged(nameof(SourceText));
        OnChanged(nameof(StatusBrush));
    }

    private bool IsErrorStatus =>
        Status.StartsWith("خطا", StringComparison.OrdinalIgnoreCase) ||
        Status.StartsWith("Error", StringComparison.OrdinalIgnoreCase);
}
