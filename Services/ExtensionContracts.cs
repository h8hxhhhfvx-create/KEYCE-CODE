namespace KeyceCode.Services;

public interface IKeyceExtension
{
    string Name { get; }
    void Initialize(IExtensionContext context);
}

public interface IExtensionContext
{
    string? WorkspacePath { get; }
    void RegisterCommand(string id, string label, Action action);
}

public sealed record ExtensionCommand(string Id, string Label, Action Action);
public sealed record ExtensionDescriptor(string Name, string AssemblyPath, bool Loaded, string? Error);
