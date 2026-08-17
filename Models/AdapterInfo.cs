namespace DNSYar.Models;

public sealed class AdapterInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Display => $"{Name} — {Description}";
    public override string ToString() => Display;
}
