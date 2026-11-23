using KeyceCode.Models;
using System.Net.Http;

namespace KeyceCode.Services;

public static class AiProviderFactory
{
    public const string GeminiKey1 = "GeminiApiKey1";
    public const string GeminiKey2 = "GeminiApiKey2";
    public const string GroqKey1 = "GroqApiKey1";
    public const string GroqKey2 = "GroqApiKey2";

    public static AiRouterService Create(AppSettings settings, ISecretStore secrets, HttpClient? httpClient = null)
    {
        var client = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var providers = new List<(AiProviderConfiguration, IAiChatProvider)>();
        Add(providers, "Gemini-1", "https://generativelanguage.googleapis.com/v1beta/openai", settings.GeminiModel, secrets.Get(GeminiKey1), client);
        Add(providers, "Gemini-2", "https://generativelanguage.googleapis.com/v1beta/openai", settings.GeminiModel, secrets.Get(GeminiKey2), client);
        Add(providers, "Groq-1", "https://api.groq.com/openai/v1", settings.GroqModel, secrets.Get(GroqKey1), client);
        Add(providers, "Groq-2", "https://api.groq.com/openai/v1", settings.GroqModel, secrets.Get(GroqKey2), client);
        return new AiRouterService(providers);
    }

    private static void Add(List<(AiProviderConfiguration, IAiChatProvider)> providers, string name, string endpoint, string model, string? key, HttpClient client)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        providers.Add((new AiProviderConfiguration(name, endpoint, model, key), new OpenAiCompatibleChatProvider(name, endpoint, key, client)));
    }
}
