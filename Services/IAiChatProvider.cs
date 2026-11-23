namespace KeyceCode.Services;

public interface IAiChatProvider
{
    string Name { get; }

    Task<string> ChatAsync(
        string model,
        IReadOnlyList<(string Role, string Content)> messages,
        CancellationToken cancellationToken = default);
}
