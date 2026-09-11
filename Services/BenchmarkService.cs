using DNSYar.Models;

namespace DNSYar.Services;

public sealed class BenchmarkService
{
    private readonly PingService _ping = new();
    private readonly CustomDnsClient _dns = new();
    private readonly ServiceProbeService _probe;

    public BenchmarkService() => _probe = new ServiceProbeService(_dns);

    public async Task TestProviderAsync(DnsProvider provider, IReadOnlyList<ServiceTarget> targets, CancellationToken cancellationToken)
    {
        provider.Status = UiText.Current.Testing;
        provider.ServiceResults.Clear();

        var ping = await _ping.TestAsync(provider.Primary, 3, 1100);
        provider.PingMs = ping.AverageMs;
        provider.PacketLoss = ping.PacketLoss;

        var dnsSamples = new List<double>();
        foreach (var host in new[] { "chatgpt.com", "gemini.google.com", "github.com", "store.steampowered.com" })
        {
            var result = await _dns.QueryAAsync(provider.Primary, host, 2200, cancellationToken);
            if (result.Success) dnsSamples.Add(result.ElapsedMs);
        }
        provider.DnsMs = dnsSamples.Count == 0 ? null : dnsSamples.Average();

        var semaphore = new SemaphoreSlim(7);
        var probeTasks = targets.Select(async target =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var ok = await _probe.ProbeAsync(provider.Primary, target, cancellationToken);
                lock (provider.ServiceResults) provider.ServiceResults[target.Id] = ok;
            }
            finally { semaphore.Release(); }
        });
        await Task.WhenAll(probeTasks);

        provider.TotalServices = targets.Count;
        provider.ReachableServices = provider.ServiceResults.Count(x => x.Value);
        provider.Score = CalculateScore(provider, targets);
        provider.Recommendation = BuildRecommendation(provider, targets);
        provider.ServiceDetailsText = BuildServiceDetails(provider, targets);
        provider.Status = provider.Score >= 85 ? UiText.Current.StatusExcellent
            : provider.Score >= 70 ? UiText.Current.StatusGood
            : provider.Score >= 45 ? UiText.Current.StatusFair
            : UiText.Current.StatusWeak;
    }

    private static int CalculateScore(DnsProvider p, IReadOnlyList<ServiceTarget> targets)
    {
        var serviceRate = p.TotalServices == 0 ? 0 : 100d * p.ReachableServices / p.TotalServices;
        var dnsScore = p.DnsMs is null ? 0 : Math.Clamp(120 - p.DnsMs.Value, 0, 100);
        var pingScore = p.PingMs is null ? 35 : Math.Clamp(115 - p.PingMs.Value, 0, 100);
        var lossScore = Math.Clamp(100 - p.PacketLoss * 2, 0, 100);
        var gameOnly = targets.Count > 0 && targets.All(x => x.Category.Equals("game", StringComparison.OrdinalIgnoreCase));

        // Gaming gives a little more weight to latency/loss, while AI/dev focus more on reachability.
        var score = gameOnly
            ? serviceRate * 0.55 + dnsScore * 0.15 + pingScore * 0.20 + lossScore * 0.10
            : serviceRate * 0.62 + dnsScore * 0.18 + pingScore * 0.10 + lossScore * 0.10;
        return (int)Math.Round(Math.Clamp(score, 0, 100));
    }

    private static string BuildRecommendation(DnsProvider provider, IReadOnlyList<ServiceTarget> targets)
    {
        double Rate(string category)
        {
            var ids = targets.Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).Select(x => x.Id).ToArray();
            if (ids.Length == 0) return 0;
            return ids.Count(id => provider.ServiceResults.TryGetValue(id, out var ok) && ok) / (double)ids.Length;
        }

        var ai = Rate("ai");
        var dev = Rate("dev");
        var game = Rate("game");
        var onlyGame = targets.Count > 0 && targets.All(x => x.Category.Equals("game", StringComparison.OrdinalIgnoreCase));

        if (onlyGame)
        {
            if (game >= .85) return UiText.Current.G("RecGameStrong");
            if (game >= .60) return UiText.Current.G("RecGameOk");
            return UiText.Current.G("RecGameNo");
        }

        if (ai >= .85 && dev >= .85 && game >= .75) return UiText.Current.G("RecAll");
        if (ai >= .85 && dev >= .85) return UiText.Current.G("RecAiDev");
        if (ai >= .85) return UiText.Current.G("RecAi");
        if (dev >= .85) return UiText.Current.G("RecDev");
        if (game >= .85) return UiText.Current.G("RecGame");
        if (ai >= .60 || dev >= .60 || game >= .60) return UiText.Current.G("RecMixed");
        return UiText.Current.G("RecNo");
    }

    private static string BuildServiceDetails(DnsProvider provider, IReadOnlyList<ServiceTarget> targets)
    {
        if (targets.Count == 0) return UiText.Current.G("NoTargets");

        var isGameOnly = targets.All(x => x.Category.Equals("game", StringComparison.OrdinalIgnoreCase));
        if (isGameOnly)
        {
            var groups = targets
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Group) ? x.Name : x.Group!, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var items = group.ToList();
                    var primary = items.FirstOrDefault(x => x.IsPrimary) ?? items[0];
                    var primaryOk = provider.ServiceResults.TryGetValue(primary.Id, out var ok) && ok;
                    var passed = items.Count(x => provider.ServiceResults.TryGetValue(x.Id, out var xok) && xok);
                    var mark = primaryOk ? "🟢" : "🔴";
                    return $"{mark} {group.Key} ({passed}/{items.Count})";
                });
            return string.Join("   •   ", groups);
        }

        if (targets.Count <= 12)
        {
            return string.Join("    ", targets.Select(target =>
            {
                var ok = provider.ServiceResults.TryGetValue(target.Id, out var reachable) && reachable;
                return $"{(ok ? "🟢" : "🔴")} {target.Name}";
            }));
        }

        var summaries = targets
            .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var total = group.Count();
                var passed = group.Count(x => provider.ServiceResults.TryGetValue(x.Id, out var ok) && ok);
                var title = group.Key switch { "ai" => "AI", "dev" => "Dev", "game" => "Game", _ => group.Key };
                return $"{title}: {passed}/{total}";
            });
        return string.Join("   •   ", summaries);
    }
}
