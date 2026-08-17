using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace DNSYar.Services;

public sealed record SiteProbeResult(
    bool DnsResolved,
    bool Reachable,
    double DnsElapsedMs,
    double HttpElapsedMs,
    int StatusCode,
    IReadOnlyList<IPAddress> Addresses,
    Uri? FinalUri,
    string? Error);

public sealed class SiteProbeService
{
    private readonly CustomDnsClient _dns = new();

    public async Task<SiteProbeResult> ProbeAsync(string dnsServer, Uri uri, CancellationToken cancellationToken)
    {
        var dnsResult = await _dns.QueryAAsync(dnsServer, uri.Host, 2800, cancellationToken);
        if (!dnsResult.Success)
            return new(false, false, dnsResult.ElapsedMs, 0, 0, Array.Empty<IPAddress>(), null, "پاسخی برای دامنه دریافت نشد");

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 6,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(4),
            PooledConnectionLifetime = TimeSpan.FromSeconds(8),
            ConnectCallback = async (context, token) =>
            {
                var resolved = await _dns.QueryAAsync(dnsServer, context.DnsEndPoint.Host, 2800, token);
                if (!resolved.Success)
                    throw new SocketException((int)SocketError.HostNotFound);

                Exception? last = null;
                foreach (var ip in resolved.Addresses)
                {
                    var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(ip, context.DnsEndPoint.Port), token);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        socket.Dispose();
                    }
                }

                throw last ?? new SocketException((int)SocketError.HostUnreachable);
            }
        };

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(9)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DNSYar/0.3 Windows");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/json;q=0.9,*/*;q=0.8");

        var sw = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            sw.Stop();

            var code = (int)response.StatusCode;
            // A 403 often represents geo/sanction blocking, so website testing marks it red.
            // 451 explicitly represents legal/access restriction and is also a failure.
            var reachable = code is >= 200 and < 500 && code is not 403 and not 451;
            return new(
                true,
                reachable,
                dnsResult.ElapsedMs,
                sw.Elapsed.TotalMilliseconds,
                code,
                dnsResult.Addresses,
                response.RequestMessage?.RequestUri,
                reachable ? null : $"سایت پاسخ HTTP {code} داد");
        }
        catch (Exception ex)
        {
            sw.Stop();
            var message = ex is TaskCanceledException ? "مهلت اتصال تمام شد" : ex.Message;
            return new(true, false, dnsResult.ElapsedMs, sw.Elapsed.TotalMilliseconds, 0, dnsResult.Addresses, null, message);
        }
    }
}
