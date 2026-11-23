using System.Net.Http.Headers;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyceCode.Services;

public sealed class OpenAiCompatibleChatProvider : IAiChatProvider
{
    private readonly string endpoint;
    private readonly string apiKey;
    private readonly HttpClient httpClient;

    public OpenAiCompatibleChatProvider(string name, string endpoint, string apiKey, HttpClient? httpClient = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Le nom du fournisseur est vide.", nameof(name)) : name;
        this.endpoint = string.IsNullOrWhiteSpace(endpoint) ? throw new ArgumentException("L’adresse du fournisseur est vide.", nameof(endpoint)) : endpoint.TrimEnd('/');
        this.apiKey = string.IsNullOrWhiteSpace(apiKey) ? throw new ArgumentException("La clé du fournisseur est vide.", nameof(apiKey)) : apiKey.Trim();
        this.httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
    }

    public string Name { get; }

    public async Task<string> ChatAsync(string model, IReadOnlyList<(string Role, string Content)> messages, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("Le modèle est vide.", nameof(model));
        if (messages.Count == 0) throw new ArgumentException("La conversation est vide.", nameof(messages));

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint + "/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new ChatRequest(model, messages.Select(m => new ChatMessage(m.Role, m.Content)).ToArray()));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{Name} a répondu {(int)response.StatusCode}.");

        var result = JsonSerializer.Deserialize<ChatResponse>(body);
        var content = result?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content)) throw new InvalidOperationException($"{Name} n’a renvoyé aucune réponse.");
        return content.Trim();
    }

    private sealed record ChatRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("messages")] ChatMessage[] Messages);
    private sealed record ChatMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);
    private sealed record ChatResponse([property: JsonPropertyName("choices")] ChatChoice[]? Choices);
    private sealed record ChatChoice([property: JsonPropertyName("message")] ChatMessage? Message);
}
