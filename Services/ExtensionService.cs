using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace KeyceCode.Services;

public sealed class ExtensionService
{
    public static readonly IReadOnlySet<string> SupportedCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "commands", "languages", "themes", "views", "tasks" };
    public static readonly IReadOnlySet<string> SupportedPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "workspace.read", "workspace.write", "terminal", "network" };
    private readonly ExtensionContributionRegistry contributions = new();
    private readonly List<AssemblyLoadContext> loadContexts = new();

    public ExtensionService(string? extensionsDirectory = null)
    {
        ExtensionsDirectory = extensionsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeyceCode", "extensions");
    }

    public string ExtensionsDirectory { get; }
    public IReadOnlyList<ExtensionCommand> Commands => contributions.Commands;
    public IReadOnlyList<ExtensionLanguage> Languages => contributions.Languages;
    public IReadOnlyList<ExtensionTheme> Themes => contributions.Themes;
    public IReadOnlyList<ExtensionView> Views => contributions.Views;
    public IReadOnlyList<ExtensionTask> Tasks => contributions.Tasks;

    public IReadOnlyList<ExtensionDescriptor> Load(string? workspacePath)
    {
        Directory.CreateDirectory(ExtensionsDirectory);
        contributions.Clear();
        foreach (var context in loadContexts) context.Unload();
        loadContexts.Clear();
        var results = new List<ExtensionDescriptor>();
        foreach (var extensionDirectory in Directory.EnumerateDirectories(ExtensionsDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (extensionDirectory.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)) continue;
            var manifestPath = Path.Combine(extensionDirectory, "manifest.json");
            if (!TryReadManifest(manifestPath, out var manifest, out var manifestError))
            {
                results.Add(new ExtensionDescriptor(Path.GetFileName(extensionDirectory), manifestPath, false, manifestError));
                continue;
            }

            var assemblyPath = Path.GetFullPath(Path.Combine(extensionDirectory, manifest.EntryAssembly));
            if (!File.Exists(assemblyPath))
            {
                results.Add(ToDescriptor(manifest, assemblyPath, false, "L’assembly d’entrée est introuvable."));
                continue;
            }
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
                    results.Add(ToDescriptor(manifest, assemblyPath, false, "Aucune classe IKeyceExtension trouvée."));
                    continue;
                }
                var extensionContributions = new ExtensionContributionRegistry();
                var context = new ExtensionContext(workspacePath, extensionContributions, manifest);
                var instances = types.Select(type => (IKeyceExtension)Activator.CreateInstance(type)!).ToArray();
                foreach (var extension in instances)
                    extension.Initialize(context);
                contributions.MergeFrom(extensionContributions);
                loadContexts.Add(loadContext);
                loadContext = null;
                results.Add(ToDescriptor(manifest, assemblyPath, true, null) with
                {
                    Name = string.Join(", ", instances.Select(extension => extension.Name))
                });
            }
            catch (Exception ex)
            {
                loadContext?.Unload();
                results.Add(ToDescriptor(manifest, assemblyPath, false, ex.Message));
            }
        }
        return results;
    }

    private static ExtensionDescriptor ToDescriptor(ExtensionManifest manifest, string assemblyPath, bool loaded, string? error) =>
        new(manifest.Name, assemblyPath, loaded, error)
        {
            Id = manifest.Id,
            Version = manifest.Version,
            Capabilities = manifest.Capabilities,
            Permissions = manifest.Permissions
        };

    public static bool TryReadManifest(string path, out ExtensionManifest manifest, out string? error)
    {
        manifest = null!;
        error = null;
        try
        {
            if (!File.Exists(path)) { error = "manifest.json absent."; return false; }
            var document = JsonSerializer.Deserialize<ManifestDocument>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (document is null) { error = "Manifest vide."; return false; }
            if (string.IsNullOrWhiteSpace(document.Id) || !IsValidId(document.Id)) { error = "Identifiant de manifeste invalide."; return false; }
            if (string.IsNullOrWhiteSpace(document.Name)) { error = "Nom d’extension obligatoire."; return false; }
            if (!Version.TryParse(document.Version, out _)) { error = "Version d’extension invalide."; return false; }
            if (string.IsNullOrWhiteSpace(document.EntryAssembly) ||
                !string.Equals(Path.GetExtension(document.EntryAssembly), ".dll", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(document.EntryAssembly) != document.EntryAssembly)
            { error = "Assembly d’entrée invalide."; return false; }

            var capabilities = (document.Capabilities ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var permissions = (document.Permissions ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var unknownCapability = capabilities.FirstOrDefault(item => !SupportedCapabilities.Contains(item));
            if (unknownCapability is not null) { error = $"Capacité non autorisée : {unknownCapability}."; return false; }
            var unknownPermission = permissions.FirstOrDefault(item => !SupportedPermissions.Contains(item));
            if (unknownPermission is not null) { error = $"Permission non autorisée : {unknownPermission}."; return false; }
            manifest = new ExtensionManifest(document.Id, document.Name.Trim(), document.Version.Trim(), document.EntryAssembly, capabilities, permissions);
            return true;
        }
        catch (JsonException) { error = "Manifest JSON invalide."; return false; }
        catch (IOException) { error = "Manifest inaccessible."; return false; }
        catch (UnauthorizedAccessException) { error = "Manifest inaccessible."; return false; }
    }

    private static bool IsValidId(string value) => value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_');

    private sealed class ManifestDocument
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? EntryAssembly { get; set; }
        public string[]? Capabilities { get; set; }
        public string[]? Permissions { get; set; }
    }

    private sealed class ExtensionContext(string? workspacePath, ExtensionContributionRegistry contributions, ExtensionManifest manifest) : IExtensionContext
    {
        public string? WorkspacePath { get; } = workspacePath;
        public ExtensionManifest Manifest { get; } = manifest;
        public bool HasPermission(string permission) => Manifest.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        public void RegisterCommand(string id, string label, Action action)
        {
            Require("commands");
            contributions.RegisterCommand(id, label, action);
        }

        public void RegisterLanguage(string id, string displayName, IReadOnlyCollection<string> extensions) { Require("languages"); contributions.RegisterLanguage(id, displayName, extensions); }
        public void RegisterTheme(string id, string displayName) { Require("themes"); contributions.RegisterTheme(id, displayName); }
        public void RegisterView(string id, string title) { Require("views"); contributions.RegisterView(id, title); }
        public void RegisterTask(string id, string label, Func<CancellationToken, Task> executeAsync) { Require("tasks"); contributions.RegisterTask(id, label, executeAsync); }

        private void Require(string capability)
        {
            if (!Manifest.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"L’extension doit déclarer la capacité {capability}.");
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
