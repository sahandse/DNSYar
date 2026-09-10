using System.Net;
using System.Text.Json;
using DNSYar.Models;

namespace DNSYar.Services;

public sealed record CatalogUpdateResult(List<DnsProvider> Providers, string SourceUrl, string Format);

public sealed class CatalogUpdater
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<CatalogUpdateResult> DownloadAsync(string url, CancellationToken cancellationToken = default)
    {
        var normalizedUrl = NormalizeGithubUrl(url);
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new InvalidOperationException("آدرس بروزرسانی باید یک URL معتبر HTTPS باشد.");

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DNSYar/0.7.2");
        var content = await client.GetStringAsync(uri, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("فهرست DNS دریافتی خالی است.");

        var trimmed = content.TrimStart();
        List<DnsProvider> list;
        string format;

        if (trimmed.StartsWith("[", StringComparison.Ordinal) || trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            list = ParseJson(content);
            format = "JSON";
        }
        else
        {
            list = ParseDnsText(content);
            format = "GitHub TXT";
        }

        list = list
            .Where(IsValidProvider)
            .GroupBy(x => x.Primary, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (list.Count == 0)
            throw new InvalidOperationException("هیچ DNS معتبر IPv4 در منبع GitHub پیدا نشد.");

        foreach (var provider in list)
        {
            provider.IsCustom = false;
            provider.Source = "GitHub";
            if (provider.Categories.Length == 0)
                provider.Categories = new[] { "all" };
        }

        return new CatalogUpdateResult(list, normalizedUrl, format);
    }

    public static string NormalizeGithubUrl(string url)
    {
        var value = url.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return value;
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return value;

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        // https://github.com/owner/repo/blob/branch/path -> raw.githubusercontent.com/owner/repo/branch/path
        if (parts.Length >= 5 && parts[2].Equals("blob", StringComparison.OrdinalIgnoreCase))
            return $"https://raw.githubusercontent.com/{parts[0]}/{parts[1]}/{parts[3]}/{string.Join("/", parts.Skip(4))}";

        return value;
    }

    private static List<DnsProvider> ParseJson(string json)
    {
        try
        {
            var list = JsonSerializer.Deserialize<List<DnsProvider>>(json, JsonOptions);
            if (list is not null) return list;
        }
        catch (JsonException) { }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("dns", out var dns))
                return JsonSerializer.Deserialize<List<DnsProvider>>(dns.GetRawText(), JsonOptions) ?? new();
        }
        catch (JsonException) { }

        throw new InvalidOperationException("ساختار JSON منبع DNS پشتیبانی نمی‌شود.");
    }

    private static List<DnsProvider> ParseDnsText(string text)
    {
        var result = new List<DnsProvider>();
        var category = "Unknown";
        foreach (var rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            if (line.StartsWith("Category:", StringComparison.OrdinalIgnoreCase))
            {
                category = line[(line.IndexOf(':') + 1)..].Trim();
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var name = line[..colon].Trim();
            var values = line[(colon + 1)..]
                .Split(',', StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Where(IsIpv4)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToArray();
            if (values.Length == 0) continue;

            result.Add(new DnsProvider
            {
                Id = $"github-{Slug(name)}-{values[0].Replace('.', '-')}",
                Name = name,
                Primary = values[0],
                Secondary = values.Length > 1 ? values[1] : null,
                Categories = new[] { "all" },
                Description = $"افزوده‌شده خودکار از GitHub • دسته منبع: {category}",
                Source = $"GitHub • {category}"
            });
        }
        return result;
    }

    private static bool IsValidProvider(DnsProvider provider) =>
        !string.IsNullOrWhiteSpace(provider.Name) && IsIpv4(provider.Primary) &&
        (string.IsNullOrWhiteSpace(provider.Secondary) || IsIpv4(provider.Secondary));

    private static bool IsIpv4(string value) =>
        IPAddress.TryParse(value, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
        && !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.Broadcast);

    private static string Slug(string value) => string.Concat(value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
}
