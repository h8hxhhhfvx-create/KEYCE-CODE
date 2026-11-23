using System.Reflection;
using System.Runtime.Loader;

namespace KeyceCode.Services;

public sealed class ExtensionService
{
    private readonly List<ExtensionCommand> commands = new();
    private readonly List<AssemblyLoadContext> loadContexts = new();

    public ExtensionService(string? extensionsDirectory = null)
    {
        ExtensionsDirectory = extensionsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeyceCode", "extensions");
    }

    public string ExtensionsDirectory { get; }
    public IReadOnlyList<ExtensionCommand> Commands => commands;

    public IReadOnlyList<ExtensionDescriptor> Load(string? workspacePath)
    {
        Directory.CreateDirectory(ExtensionsDirectory);
        commands.Clear();
        foreach (var context in loadContexts) context.Unload();
        loadContexts.Clear();
        var results = new List<ExtensionDescriptor>();
        foreach (var assemblyPath in Directory.EnumerateFiles(ExtensionsDirectory, "*.dll", SearchOption.TopDirectoryOnly).OrderBy(path => path))
        {
            AssemblyLoadContext? loadContext = null;
            try
            {
                loadContext = new ExtensionLoadContext(Path.GetFileNameWithoutExtension(assemblyPath));
                var assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
                var types = assembly.GetTypes().Where(type => typeof(IKeyceExtension).IsAssignableFrom(type) && !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) is not null).ToArray();
                if (types.Length == 0)
                {
                    loadContext.Unload();
                    loadContext = null;
                    results.Add(new ExtensionDescriptor(Path.GetFileNameWithoutExtension(assemblyPath), assemblyPath, false, "Aucune classe IKeyceExtension trouvée."));
                    continue;
                }
                var context = new ExtensionContext(workspacePath, commands);
                var instances = types.Select(type => (IKeyceExtension)Activator.CreateInstance(type)!).ToArray();
                foreach (var extension in instances)
                    extension.Initialize(context);
                loadContexts.Add(loadContext);
                loadContext = null;
                results.Add(new ExtensionDescriptor(string.Join(", ", instances.Select(extension => extension.Name)), assemblyPath, true, null));
            }
            catch (Exception ex)
            {
                loadContext?.Unload();
                results.Add(new ExtensionDescriptor(Path.GetFileNameWithoutExtension(assemblyPath), assemblyPath, false, ex.Message));
            }
        }
        return results;
    }

    private sealed class ExtensionContext(string? workspacePath, List<ExtensionCommand> commands) : IExtensionContext
    {
        public string? WorkspacePath { get; } = workspacePath;
        public void RegisterCommand(string id, string label, Action action)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Une commande d’extension doit avoir un identifiant et un libellé.");
            commands.RemoveAll(command => string.Equals(command.Id, id, StringComparison.OrdinalIgnoreCase));
            commands.Add(new ExtensionCommand(id, label, action));
        }
    }

    private sealed class ExtensionLoadContext(string name) : AssemblyLoadContext(name, isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName)
            => string.Equals(assemblyName.Name, typeof(IKeyceExtension).Assembly.GetName().Name, StringComparison.OrdinalIgnoreCase)
                ? typeof(IKeyceExtension).Assembly
                : null;
    }
}
