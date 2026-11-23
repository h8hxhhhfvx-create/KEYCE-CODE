using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace KeyceCode.Services;

public sealed class OllamaChatService
{
    private readonly HttpClient httpClient;

    public OllamaChatService(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public async Task<string> ChatAsync(
        string endpoint,
        string model,
        IReadOnlyList<(string Role, string Content)> messages,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("L’adresse d’Ollama est vide.", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("Le modèle Ollama est vide.", nameof(model));
        if (messages.Count == 0) throw new ArgumentException("La conversation est vide.", nameof(messages));

        var url = endpoint.TrimEnd('/') + "/api/chat";
        using var response = await httpClient.PostAsJsonAsync(url, new ChatRequest(
            model,
            messages.Select(message => new ChatMessage(message.Role, message.Content)).ToArray(),
            false), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama a répondu {(int)response.StatusCode} : {body}");

        var result = System.Text.Json.JsonSerializer.Deserialize<ChatResponse>(body);
        if (string.IsNullOrWhiteSpace(result?.Message?.Content))
            throw new InvalidOperationException("Ollama n’a renvoyé aucune réponse.");
        return result.Message.Content.Trim();
    }

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] ChatMessage[] Messages,
        [property: JsonPropertyName("stream")] bool Stream);
    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);
    private sealed record ChatResponse([property: JsonPropertyName("message")] ChatMessage? Message);
}
