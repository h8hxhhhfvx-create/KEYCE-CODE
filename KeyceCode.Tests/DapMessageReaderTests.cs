using System.Text;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class DapMessageReaderTests
{
    [Fact]
    public async Task ReadsUtf8PayloadUsingByteLength()
    {
        const string payload = "{\"message\":\"Débogage réussi — prêt\"}";
        await using var stream = CreateStream(payload);

        var result = await DapMessageReader.ReadMessageAsync(stream);

        Assert.Equal(payload, result);
    }

    [Fact]
    public async Task SkipsAdditionalHeadersAndReadsFollowingMessage()
    {
        const string first = "{\"seq\":1}";
        const string second = "{\"seq\":2}";
        var bytes = Encoding.UTF8.GetBytes(
            $"Content-Type: application/vscode-jsonrpc; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(first)}\r\n\r\n{first}" +
            $"Content-Length: {Encoding.UTF8.GetByteCount(second)}\r\n\r\n{second}");
        await using var stream = new MemoryStream(bytes);

        Assert.Equal(first, await DapMessageReader.ReadMessageAsync(stream));
        Assert.Equal(second, await DapMessageReader.ReadMessageAsync(stream));
    }

    [Fact]
    public async Task ReturnsNullForTruncatedPayload()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Content-Length: 10\r\n\r\nshort"));

        Assert.Null(await DapMessageReader.ReadMessageAsync(stream));
    }

    private static MemoryStream CreateStream(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes($"Content-Length: {bytesLength(payload)}\r\n\r\n{payload}");
        return new MemoryStream(bytes);

        static int bytesLength(string value) => Encoding.UTF8.GetByteCount(value);
    }
}
