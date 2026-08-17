using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using DNSYar.Models;
using Microsoft.Win32;

namespace DNSYar.Services;

public sealed class NetworkDnsManager
{
    public IReadOnlyList<AdapterInfo> GetUsableAdapters() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up)
        .Where(n => n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
        .Select(n => new AdapterInfo { Id = n.Id, Name = n.Name, Description = n.Description })
        .OrderBy(a => a.Name)
        .ToList();

    public IReadOnlyList<string> GetCurrentDns(string adapterId)
    {
        var n = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(x => x.Id == adapterId);
        return n?.GetIPProperties().DnsAddresses
                   .Where(x => x.AddressFamily == AddressFamily.InterNetwork)
                   .Select(x => x.ToString())
                   .ToList()
               ?? new List<string>();
    }

    /// <summary>
    /// Captures the DNS configuration before DNSYar changes it. We inspect the
    /// adapter registry key to distinguish a static DNS from DHCP-provided DNS.
    /// </summary>
    public DnsSnapshot CaptureCurrentState(AdapterInfo adapter)
    {
        var current = GetCurrentDns(adapter.Id).ToList();
        var staticValue = ReadStaticNameServer(adapter.Id);
        var isDhcp = string.IsNullOrWhiteSpace(staticValue);

        var servers = isDhcp
            ? current
            : SplitServers(staticValue!);

        return new DnsSnapshot
        {
            AdapterId = adapter.Id,
            AdapterName = adapter.Name,
            IsDhcp = isDhcp,
            Servers = servers,
            CapturedAt = DateTimeOffset.Now
        };
    }

    public async Task ApplyAsync(AdapterInfo adapter, DnsProvider provider)
    {
        await RunNetshAsync($"interface ipv4 set dnsservers name=\"{Escape(adapter.Name)}\" static {provider.Primary} primary validate=no");
        if (!string.IsNullOrWhiteSpace(provider.Secondary))
            await RunNetshAsync($"interface ipv4 add dnsservers name=\"{Escape(adapter.Name)}\" {provider.Secondary} index=2 validate=no");
        await FlushAsync();
    }

    public async Task RestoreSnapshotAsync(DnsSnapshot snapshot)
    {
        if (snapshot.IsDhcp || snapshot.Servers.Count == 0)
        {
            await RunNetshAsync($"interface ipv4 set dnsservers name=\"{Escape(snapshot.AdapterName)}\" source=dhcp");
        }
        else
        {
            await RunNetshAsync($"interface ipv4 set dnsservers name=\"{Escape(snapshot.AdapterName)}\" static {snapshot.Servers[0]} primary validate=no");
            for (var i = 1; i < snapshot.Servers.Count; i++)
                await RunNetshAsync($"interface ipv4 add dnsservers name=\"{Escape(snapshot.AdapterName)}\" {snapshot.Servers[i]} index={i + 1} validate=no");
        }
        await FlushAsync();
    }

    public void RestoreSnapshot(DnsSnapshot snapshot)
    {
        if (snapshot.IsDhcp || snapshot.Servers.Count == 0)
        {
            RunNetsh($"interface ipv4 set dnsservers name=\"{Escape(snapshot.AdapterName)}\" source=dhcp");
        }
        else
        {
            RunNetsh($"interface ipv4 set dnsservers name=\"{Escape(snapshot.AdapterName)}\" static {snapshot.Servers[0]} primary validate=no");
            for (var i = 1; i < snapshot.Servers.Count; i++)
                RunNetsh($"interface ipv4 add dnsservers name=\"{Escape(snapshot.AdapterName)}\" {snapshot.Servers[i]} index={i + 1} validate=no");
        }
        Flush();
    }

    public async Task RestoreDhcpAsync(AdapterInfo adapter)
    {
        await RunNetshAsync($"interface ipv4 set dnsservers name=\"{Escape(adapter.Name)}\" source=dhcp");
        await FlushAsync();
    }

    private static string? ReadStaticNameServer(string adapterId)
    {
        var trimmed = adapterId.Trim('{', '}');
        var candidates = new[] { adapterId, $"{{{trimmed}}}", trimmed }.Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var id in candidates)
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{id}");
            if (key is null) continue;
            return key.GetValue("NameServer")?.ToString();
        }
        return null;
    }

    private static List<string> SplitServers(string value) => value
        .Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static string Escape(string value) => value.Replace("\"", "\\\"");

    private static async Task RunNetshAsync(string arguments)
    {
        var psi = new ProcessStartInfo("netsh", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("netsh اجرا نشد.");
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            var error = await p.StandardError.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(error)) error = await p.StandardOutput.ReadToEndAsync();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "تغییر DNS ناموفق بود." : error.Trim());
        }
    }

    private static void RunNetsh(string arguments)
    {
        var psi = new ProcessStartInfo("netsh", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("netsh اجرا نشد.");
        p.WaitForExit(4000);
        if (!p.HasExited)
        {
            try { p.Kill(true); } catch { }
            throw new TimeoutException("زمان بازگردانی DNS تمام شد.");
        }
        if (p.ExitCode != 0)
        {
            var error = p.StandardError.ReadToEnd();
            if (string.IsNullOrWhiteSpace(error)) error = p.StandardOutput.ReadToEnd();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "بازگردانی DNS ناموفق بود." : error.Trim());
        }
    }

    private static async Task FlushAsync()
    {
        var psi = new ProcessStartInfo("ipconfig", "/flushdns") { UseShellExecute = false, CreateNoWindow = true };
        using var p = Process.Start(psi);
        if (p is not null) await p.WaitForExitAsync();
    }

    private static void Flush()
    {
        var psi = new ProcessStartInfo("ipconfig", "/flushdns") { UseShellExecute = false, CreateNoWindow = true };
        using var p = Process.Start(psi);
        if (p is not null) p.WaitForExit(2500);
    }
}
