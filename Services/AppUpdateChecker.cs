using System.Net.Http;
using System.Text.Json;

namespace DNSYar.Services;

public sealed record UpdateCheckResult(bool UpdateAvailable, string LatestVersion, string ReleaseUrl);

public sealed class AppUpdateChecker
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/sahandse/DNSYar/releases/latest";

    public async Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DNSYar-UpdateChecker");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        using var response = await client.GetAsync(LatestReleaseApiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var tag = doc.RootElement.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
        var releaseUrl = doc.RootElement.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "";

        var versionText = tag.TrimStart('v', 'V');
        var updateAvailable = Version.TryParse(versionText, out var latest) && latest > currentVersion;

        return new UpdateCheckResult(updateAvailable, versionText, releaseUrl);
    }
}
