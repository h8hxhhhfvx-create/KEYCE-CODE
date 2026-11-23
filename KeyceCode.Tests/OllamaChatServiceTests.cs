using System.Net;
using System.Net.Http.Json;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class OllamaChatServiceTests
{
    [Fact]
    public async Task ChatAsync_ReturnsAssistantContent()
    {
        using var client = new HttpClient(new FakeHandler("{\"message\":{\"role\":\"assistant\",\"content\":\"Bonjour\"}}"));
        var answer = await new OllamaChatService(client).ChatAsync("http://localhost:11434", "qwen2.5:7b", [("user", "Bonjour")]);
        Assert.Equal("Bonjour", answer);
    }

    private sealed class FakeHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(System.Text.Json.JsonSerializer.Deserialize<object>(body)) });
    }
}
