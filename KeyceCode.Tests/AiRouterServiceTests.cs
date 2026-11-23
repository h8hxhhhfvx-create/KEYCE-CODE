using KeyceCode.Models;
using KeyceCode.Services;
using Xunit;
using System.Net;
using System.Net.Http;

namespace KeyceCode.Tests;

public sealed class AiRouterServiceTests
{
    [Fact]
    public async Task UsesNextProviderWhenFirstFails()
    {
        var first = new FakeProvider("Gemini-1", fail: true);
        var second = new FakeProvider("Groq-1", fail: false);
        var router = new AiRouterService([
            (new AiProviderConfiguration("Gemini-1", "https://example.invalid", "gemini-test", "secret-a"), first),
            (new AiProviderConfiguration("Groq-1", "https://example.invalid", "groq-test", "secret-b"), second)]);

        var result = await router.ChatAsync([("user", "Bonjour")]);

        Assert.Equal("Groq-1", result.Provider);
        Assert.Equal(1, first.Calls);
        Assert.Equal(1, second.Calls);
    }

    [Fact]
    public async Task DoesNotExposeApiKeysInFailure()
    {
        var router = new AiRouterService([(
            new AiProviderConfiguration("Gemini-1", "https://example.invalid", "model", "PRIVATE_KEY"),
            new FakeProvider("Gemini-1", fail: true))], TimeSpan.Zero);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => router.ChatAsync([("user", "x")]));

        Assert.DoesNotContain("PRIVATE_KEY", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiCompatibleAdapterSendsBearerAndReadsChoice()
    {
        HttpRequestMessage? captured = null;
        string? requestBody = null;
        using var client = new HttpClient(new CaptureHandler(request =>
        {
            captured = request;
            requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}]}")
            };
        }));
        var provider = new OpenAiCompatibleChatProvider("Groq-1", "https://api.test/v1", "PRIVATE_KEY", client);

        var answer = await provider.ChatAsync("test-model", [("user", "Bonjour")]);

        Assert.Equal("ok", answer);
        Assert.Equal("Bearer PRIVATE_KEY", captured!.Headers.Authorization!.ToString());
        Assert.Contains("test-model", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public void FactoryKeepsConfiguredOrderAndSkipsMissingSecrets()
    {
        var store = new FakeSecretStore
        {
            [AiProviderFactory.GeminiKey1] = "g1",
            [AiProviderFactory.GroqKey2] = "q2"
        };

        var router = AiProviderFactory.Create(new KeyceCode.Models.AppSettings(), store);

        Assert.Equal(["Gemini-1", "Groq-2"], router.ProviderNames);
    }

    [Fact]
    public void CredentialTargetRejectsInvalidSecretNames()
    {
        Assert.Equal("KeyceCode/GeminiApiKey1", WindowsCredentialStore.Target("GeminiApiKey1"));
        Assert.Throws<ArgumentException>(() => WindowsCredentialStore.Target("Gemini/ApiKey1"));
    }

    private sealed class CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class FakeSecretStore : Dictionary<string, string>, ISecretStore
    {
        public string? Get(string name) => TryGetValue(name, out var value) ? value : null;
        public void Set(string name, string value) => this[name] = value;
        public void Delete(string name) => Remove(name);
    }

    private sealed class FakeProvider(string name, bool fail) : IAiChatProvider
    {
        public string Name { get; } = name;
        public int Calls { get; private set; }
        public Task<string> ChatAsync(string model, IReadOnlyList<(string Role, string Content)> messages, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (fail) throw new InvalidOperationException("simulated failure");
            return Task.FromResult("réponse de test");
        }
    }
}
