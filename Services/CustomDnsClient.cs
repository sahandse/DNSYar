using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DNSYar.Services;

public sealed record DnsQueryResult(double ElapsedMs, IReadOnlyList<IPAddress> Addresses, bool Success);

public sealed class CustomDnsClient
{
    public async Task<DnsQueryResult> QueryAAsync(string dnsServer, string host, int timeoutMs = 2500, CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(dnsServer, out var serverIp))
            return new(0, Array.Empty<IPAddress>(), false);

        var packet = BuildQuery(host);
        using var udp = new UdpClient(serverIp.AddressFamily);
        udp.Connect(new IPEndPoint(serverIp, 53));

        var sw = Stopwatch.StartNew();
        try
        {
            await udp.SendAsync(packet, packet.Length);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);
            var result = await udp.ReceiveAsync(timeout.Token);
            sw.Stop();
            var addresses = ParseARecords(result.Buffer);
            return new(sw.Elapsed.TotalMilliseconds, addresses, addresses.Count > 0);
        }
        catch
        {
            sw.Stop();
            return new(sw.Elapsed.TotalMilliseconds, Array.Empty<IPAddress>(), false);
        }
    }

    private static byte[] BuildQuery(string host)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        var id = (ushort)Random.Shared.Next(ushort.MaxValue);
        WriteUInt16BE(bw, id);
        WriteUInt16BE(bw, 0x0100); // recursion desired
        WriteUInt16BE(bw, 1); // questions
        WriteUInt16BE(bw, 0);
        WriteUInt16BE(bw, 0);
        WriteUInt16BE(bw, 0);

        foreach (var label in host.Trim('.').Split('.'))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            bw.Write((byte)bytes.Length);
            bw.Write(bytes);
        }
        bw.Write((byte)0);
        WriteUInt16BE(bw, 1); // A
        WriteUInt16BE(bw, 1); // IN
        return ms.ToArray();
    }

    private static List<IPAddress> ParseARecords(byte[] data)
    {
        var addresses = new List<IPAddress>();
        if (data.Length < 12) return addresses;

        var qd = ReadUInt16BE(data, 4);
        var an = ReadUInt16BE(data, 6);
        var offset = 12;

        for (var i = 0; i < qd; i++)
        {
            SkipName(data, ref offset);
            offset += 4;
            if (offset > data.Length) return addresses;
        }

        for (var i = 0; i < an && offset < data.Length; i++)
        {
            SkipName(data, ref offset);
            if (offset + 10 > data.Length) break;
            var type = ReadUInt16BE(data, offset); offset += 2;
            var klass = ReadUInt16BE(data, offset); offset += 2;
            offset += 4; // TTL
            var rdLength = ReadUInt16BE(data, offset); offset += 2;
            if (offset + rdLength > data.Length) break;

            if (type == 1 && klass == 1 && rdLength == 4)
                addresses.Add(new IPAddress(data.AsSpan(offset, 4)));

            offset += rdLength;
        }

        return addresses;
    }

    private static void SkipName(byte[] data, ref int offset)
    {
        while (offset < data.Length)
        {
            var len = data[offset++];
            if (len == 0) return;
            if ((len & 0xC0) == 0xC0)
            {
                offset++; // compression pointer second byte
                return;
            }
            offset += len;
        }
    }

    private static ushort ReadUInt16BE(byte[] data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));
    private static void WriteUInt16BE(BinaryWriter bw, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        bw.Write(buffer);
    }
}
