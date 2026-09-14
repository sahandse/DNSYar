namespace DNSYar.Services;

public static class CatalogUrl
{
    public static string NormalizeGithubUrl(string url)
    {
        var value = url.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return value;
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return value;

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 5 && parts[2].Equals("blob", StringComparison.OrdinalIgnoreCase))
            return $"https://raw.githubusercontent.com/{parts[0]}/{parts[1]}/{parts[3]}/{string.Join("/", parts.Skip(4))}";

        return value;
    }

    public static bool IsHttps(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
