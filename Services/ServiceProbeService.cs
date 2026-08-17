using System.Net;
using System.Net.Sockets;
using DNSYar.Models;

namespace DNSYar.Services;

public sealed class ServiceProbeService
{
    private readonly CustomDnsClient _dns;
    public ServiceProbeService(CustomDnsClient dns) => _dns = dns;

    public async Task<bool> ProbeAsync(string dnsServer, ServiceTarget target, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(target.Url, UriKind.Absolute, out var uri)) return false;

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(4),
            PooledConnectionLifetime = TimeSpan.FromSeconds(10),
            ConnectCallback = async (context, token) =>
            {
                var resolved = await _dns.QueryAAsync(dnsServer, context.DnsEndPoint.Host, 2500, token);
                if (!resolved.Success) throw new SocketException((int)SocketError.HostNotFound);

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

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(7) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DNSYar/1.0 Windows");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var code = (int)response.StatusCode;
            // 401/403/404 can still prove that the target service is reachable.
            // 451 is treated as unavailable because it explicitly signals access restriction.
            return code is >= 200 and < 500 && code != 451;
        }
        catch
        {
            return false;
        }
    }
}
