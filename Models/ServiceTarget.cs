namespace DNSYar.Models;

public sealed class ServiceTarget
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "all";
    public string Url { get; set; } = "";
    public string? Group { get; set; }
    public bool IsPrimary { get; set; }
}
