namespace DNSYar.Models;

public sealed class AppSettings
{
    public const string DefaultGithubUpdateUrl = "https://raw.githubusercontent.com/mehrshadasgary/Iran-DNS-Switcher/main/dns_servers.txt";
    public bool AutoUpdate { get; set; } = true;
    public int AutoUpdateHours { get; set; } = 24;
    public string UpdateUrl { get; set; } = DefaultGithubUpdateUrl;
    public string FontFamily { get; set; } = "Auto";
    public string ThemeName { get; set; } = "Paper";
    public string Language { get; set; } = "fa";
    public bool RestoreDnsOnExit { get; set; } = true;
    public bool RecoverDnsAfterUnexpectedExit { get; set; } = true;
    public string? LastAdapterId { get; set; }
    public DateTimeOffset? LastCatalogUpdate { get; set; }
    public bool SmartAutoDnsEnabled { get; set; } = false;
    public string SmartAutoDnsProfile { get; set; } = "all";
    public int SmartAutoDnsIntervalMinutes { get; set; } = 10;
    public int SmartAutoDnsMinimumScore { get; set; } = 65;
    public int SmartAutoDnsFailuresBeforeSwitch { get; set; } = 2;
    public bool TrayEnabled { get; set; } = true;
    public bool CloseToTray { get; set; } = false;
}
