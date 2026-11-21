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
        if (!string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
        {
            editor.SyntaxHighlighting = null;
            return;
        }

        var builtIn = HighlightingManager.Instance.GetDefinition("C#");
        if (builtIn is not null)
        {
            editor.SyntaxHighlighting = builtIn;
            return;
        }

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("KeyceCode.Resources.Syntax.CSharpSyntax.xshd")
            ?? throw new InvalidOperationException("La définition de coloration C# est introuvable.");
        using var reader = XmlReader.Create(stream);
        editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}
