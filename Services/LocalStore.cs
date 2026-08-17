using System.Text.Json;
using DNSYar.Models;

namespace DNSYar.Services;

public sealed class LocalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string RootPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DNSYar");

    private string SettingsPath => Path.Combine(RootPath, "settings.json");
    private string CatalogPath => Path.Combine(RootPath, "dns.json");
    private string CustomCatalogPath => Path.Combine(RootPath, "custom-dns.json");
    private string SnapshotPath => Path.Combine(RootPath, "dns-session.json");

    public LocalStore() => Directory.CreateDirectory(RootPath);

    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (!File.Exists(SettingsPath)) return new AppSettings();
        try
        {
            await using var fs = File.OpenRead(SettingsPath);
            return await JsonSerializer.DeserializeAsync<AppSettings>(fs, JsonOptions) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        Directory.CreateDirectory(RootPath);
        await using var fs = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(fs, settings, JsonOptions);
    }

    public async Task<List<DnsProvider>> LoadProvidersAsync()
    {
        var path = File.Exists(CatalogPath)
            ? CatalogPath
            : Path.Combine(AppContext.BaseDirectory, "Data", "dns.json");

        List<DnsProvider> providers;
        await using (var fs = File.OpenRead(path))
            providers = await JsonSerializer.DeserializeAsync<List<DnsProvider>>(fs, JsonOptions) ?? new();

        var custom = await LoadCustomProvidersAsync();
        foreach (var item in custom)
        {
            item.IsCustom = true;
            item.Source = "دستی";
            if (!providers.Any(x => SameProvider(x, item))) providers.Add(item);
        }
        return providers;
    }

    // Persists only the managed/online catalog. Custom entries live in their own file
    // so a GitHub refresh can never overwrite or delete the user's own DNS entries.
    public async Task SaveProvidersAsync(IEnumerable<DnsProvider> providers)
    {
        Directory.CreateDirectory(RootPath);
        await using var fs = File.Create(CatalogPath);
        await JsonSerializer.SerializeAsync(fs, providers.Where(x => !x.IsCustom), JsonOptions);
    }

    public async Task<List<DnsProvider>> LoadCustomProvidersAsync()
    {
        if (!File.Exists(CustomCatalogPath)) return new();
        try
        {
            await using var fs = File.OpenRead(CustomCatalogPath);
            return await JsonSerializer.DeserializeAsync<List<DnsProvider>>(fs, JsonOptions) ?? new();
        }
        catch { return new(); }
    }

    public async Task SaveCustomProvidersAsync(IEnumerable<DnsProvider> providers)
    {
        Directory.CreateDirectory(RootPath);
        await using var fs = File.Create(CustomCatalogPath);
        await JsonSerializer.SerializeAsync(fs, providers.Where(x => x.IsCustom), JsonOptions);
    }

    public async Task<List<ServiceTarget>> LoadTargetsAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "services.json");
        await using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<ServiceTarget>>(fs, JsonOptions) ?? new();
    }

    public async Task<List<DnsSnapshot>> LoadDnsSnapshotsAsync()
    {
        if (!File.Exists(SnapshotPath)) return new();
        try
        {
            await using var fs = File.OpenRead(SnapshotPath);
            return await JsonSerializer.DeserializeAsync<List<DnsSnapshot>>(fs, JsonOptions) ?? new();
        }
        catch { return new(); }
    }

    public async Task SaveDnsSnapshotsAsync(IEnumerable<DnsSnapshot> snapshots)
    {
        Directory.CreateDirectory(RootPath);
        await using var fs = File.Create(SnapshotPath);
        await JsonSerializer.SerializeAsync(fs, snapshots, JsonOptions);
    }

    public void SaveDnsSnapshots(IEnumerable<DnsSnapshot> snapshots)
    {
        Directory.CreateDirectory(RootPath);
        File.WriteAllText(SnapshotPath, JsonSerializer.Serialize(snapshots, JsonOptions));
    }

    private static bool SameProvider(DnsProvider a, DnsProvider b) =>
        a.Primary.Equals(b.Primary, StringComparison.OrdinalIgnoreCase) ||
        (!string.IsNullOrWhiteSpace(a.Id) && a.Id.Equals(b.Id, StringComparison.OrdinalIgnoreCase));
}
