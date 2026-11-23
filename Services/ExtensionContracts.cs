namespace KeyceCode.Services;

public sealed record ExtensionManifest(
    string Id,
    string Name,
    string Version,
    string EntryAssembly,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Permissions);

public interface IKeyceExtension
{
    string Name { get; }
    void Initialize(IExtensionContext context);
}

public interface IExtensionContext
{
    string? WorkspacePath { get; }
    ExtensionManifest Manifest { get; }
    bool HasPermission(string permission);
    void RegisterCommand(string id, string label, Action action);
    void RegisterLanguage(string id, string displayName, IReadOnlyCollection<string> extensions);
    void RegisterTheme(string id, string displayName);
    void RegisterView(string id, string title);
    void RegisterTask(string id, string label, Func<CancellationToken, Task> executeAsync);
}

public sealed record ExtensionCommand(string Id, string Label, Action Action);
public sealed record ExtensionLanguage(string Id, string DisplayName, IReadOnlyCollection<string> Extensions);
public sealed record ExtensionTheme(string Id, string DisplayName);
public sealed record ExtensionView(string Id, string Title);
public sealed record ExtensionTask(string Id, string Label, Func<CancellationToken, Task> ExecuteAsync);
public sealed record ExtensionDescriptor(string Name, string AssemblyPath, bool Loaded, string? Error)
{
    public string? Id { get; init; }
    public string? Version { get; init; }
    public IReadOnlyList<string> Capabilities { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
