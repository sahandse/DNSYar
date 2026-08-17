namespace DNSYar.Models;

public sealed class DnsSnapshot
{
    public string AdapterId { get; set; } = "";
    public string AdapterName { get; set; } = "";
    public bool IsDhcp { get; set; }
    public List<string> Servers { get; set; } = new();
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.Now;
}
