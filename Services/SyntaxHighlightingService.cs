using System.Reflection;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace KeyceCode.Services;

public sealed class SyntaxHighlightingService
{
    public void Apply(TextEditor editor, string? extension = ".cs")
    {
        var normalizedExtension = extension?.Trim().ToLowerInvariant();
        var builtIn = string.Equals(normalizedExtension, ".cs", StringComparison.OrdinalIgnoreCase)
            ? HighlightingManager.Instance.GetDefinition("C#")
            : HighlightingManager.Instance.GetDefinitionByExtension(normalizedExtension ?? string.Empty);
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
}
