using System.Reflection;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace KeyceCode.Services;

public sealed class SyntaxHighlightingService
{
    private static readonly IReadOnlyDictionary<string, string> CustomResources =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".go"] = "GenericCodeSyntax.xshd", [".rs"] = "GenericCodeSyntax.xshd",
            [".rb"] = "GenericCodeSyntax.xshd", [".lua"] = "GenericCodeSyntax.xshd",
            [".yaml"] = "DataSyntax.xshd", [".yml"] = "DataSyntax.xshd",
            [".sh"] = "ShellSyntax.xshd", [".bash"] = "ShellSyntax.xshd",
            [".bat"] = "ShellSyntax.xshd", [".cmd"] = "ShellSyntax.xshd"
        };
    private static readonly IReadOnlyDictionary<string, string> CustomNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".go"] = "Go", [".rs"] = "Rust", [".rb"] = "Ruby", [".lua"] = "Lua",
            [".yaml"] = "YAML", [".yml"] = "YAML", [".sh"] = "Bash", [".bash"] = "Bash",
            [".bat"] = "Batch", [".cmd"] = "Batch"
        };
    private static readonly IReadOnlyDictionary<string, string> DefinitionAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"] = "C#",
            [".js"] = "JavaScript",
            [".jsx"] = "JavaScript",
            [".ts"] = "JavaScript",
            [".tsx"] = "JavaScript",
            [".html"] = "HTML",
            [".htm"] = "HTML",
            [".css"] = "CSS",
            [".json"] = "Json",
            [".xml"] = "XML",
            [".py"] = "Python",
            [".php"] = "PHP",
            [".java"] = "Java",
            [".sql"] = "TSQL",
            [".md"] = "MarkDown",
            [".ps1"] = "PowerShell",
            [".sh"] = "Bash"
        };

    public bool IsSupported(string? extension)
    {
        var normalizedExtension = NormalizeExtension(extension) ?? ".cs";
        return ResolveDefinition(normalizedExtension) is not null ||
            string.Equals(normalizedExtension, ".cs", StringComparison.OrdinalIgnoreCase);
    }

    public string? GetDefinitionName(string? extension)
    {
        var normalizedExtension = NormalizeExtension(extension) ?? ".cs";
        var definition = ResolveDefinition(normalizedExtension);
        if (definition is not null && CustomNames.TryGetValue(normalizedExtension, out var customName))
            return customName;
        return definition?.Name;
    }

    public void Apply(TextEditor editor, string? extension = ".cs")
    {
        var normalizedExtension = NormalizeExtension(extension);
        var builtIn = ResolveDefinition(normalizedExtension);
        if (builtIn is not null)
        {
            editor.SyntaxHighlighting = builtIn;
            return;
        }

        if (!string.Equals(normalizedExtension, ".cs", StringComparison.OrdinalIgnoreCase))
        {
            editor.SyntaxHighlighting = null;
            return;
        }

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("KeyceCode.Resources.Syntax.CSharpSyntax.xshd")
            ?? throw new InvalidOperationException("La définition de coloration C# est introuvable.");
        using var reader = XmlReader.Create(stream);
        editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private static string? NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return ".cs";
        var value = extension.Trim().ToLowerInvariant();
        return value.StartsWith(".", StringComparison.Ordinal) ? value : $".{value}";
    }

    private static IHighlightingDefinition? ResolveDefinition(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return null;
        var manager = HighlightingManager.Instance;
        var byExtension = manager.GetDefinitionByExtension(extension);
        if (byExtension is not null) return byExtension;
        var aliased = DefinitionAliases.TryGetValue(extension, out var name)
            ? manager.GetDefinition(name)
            : null;
        if (aliased is not null) return aliased;
        return CustomResources.TryGetValue(extension, out var resource)
            ? LoadCustomDefinition(resource)
            : null;
    }

    private static IHighlightingDefinition LoadCustomDefinition(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"KeyceCode.Resources.Syntax.{resourceName}")
            ?? throw new InvalidOperationException($"La définition de coloration {resourceName} est introuvable.");
        using var reader = XmlReader.Create(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}
