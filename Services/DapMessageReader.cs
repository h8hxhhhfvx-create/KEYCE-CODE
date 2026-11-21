using System.Text;

namespace KeyceCode.Services;

/// <summary>
/// Lit un message Debug Adapter Protocol en respectant Content-Length en octets UTF-8.
/// </summary>
internal static class DapMessageReader
{
    public static async Task<string?> ReadMessageAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var contentLength = -1;
        while (true)
        {
            var line = await ReadHeaderLineAsync(stream, cancellationToken);
            if (line is null) return null;
            if (line.Length == 0) break;

            var separator = line.IndexOf(':');
            if (separator <= 0) continue;
            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value, out var parsedLength) && parsedLength >= 0)
                contentLength = parsedLength;
        }

        if (contentLength < 0) return null;
        var payload = new byte[contentLength];
        var offset = 0;
        while (offset < payload.Length)
        {
            var read = await stream.ReadAsync(payload.AsMemory(offset, payload.Length - offset), cancellationToken);
            if (read == 0) return null;
            offset += read;
        }
        return Encoding.UTF8.GetString(payload);
    }

    private static async Task<string?> ReadHeaderLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        var singleByte = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(singleByte.AsMemory(), cancellationToken);
            if (read == 0)
                return bytes.Count == 0 ? null : Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
            if (singleByte[0] == (byte)'\n')
                return Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
            bytes.Add(singleByte[0]);
        }
    }
}
