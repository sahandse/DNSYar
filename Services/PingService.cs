using System.Net;
using System.Net.NetworkInformation;

namespace DNSYar.Services;

public sealed record PingResult(double? AverageMs, double PacketLoss);

public sealed class PingService
{
    public async Task<PingResult> TestAsync(string ip, int attempts = 3, int timeoutMs = 1200)
    {
        if (!IPAddress.TryParse(ip, out var address)) return new(null, 100);
        using var ping = new Ping();
        var replies = new List<long>();

        for (var i = 0; i < attempts; i++)
        {
            try
            {
                var reply = await ping.SendPingAsync(address, timeoutMs);
                if (reply.Status == IPStatus.Success) replies.Add(reply.RoundtripTime);
            }
            catch { }
        }

        var loss = 100d * (attempts - replies.Count) / attempts;
        return new(replies.Count == 0 ? null : replies.Average(), loss);
    }
}
