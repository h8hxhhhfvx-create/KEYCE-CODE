namespace KeyceCode.Models;

public sealed record AiProviderConfiguration(string Name, string Endpoint, string Model, string ApiKey);

public sealed record AiProviderResult(string Provider, string Model, string Content);
