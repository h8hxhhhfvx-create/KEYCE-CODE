using KeyceCode.Models;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KeyceCode.Services;

public sealed class CodeSymbolService
{
    public IReadOnlyList<CodeSymbol> GetSymbols(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source ?? string.Empty);
        return tree.GetRoot().DescendantNodes()
            .Select(TryCreateSymbol)
            .Where(symbol => symbol is not null)
            .Select(symbol => symbol!)
            .OrderBy(symbol => symbol.Line)
            .ThenBy(symbol => symbol.Column)
            .ToArray();
    }

    private static CodeSymbol? TryCreateSymbol(Microsoft.CodeAnalysis.SyntaxNode node)
    {
        var (name, kind) = node switch
        {
            ClassDeclarationSyntax item => (item.Identifier.Text, "Classe"),
            StructDeclarationSyntax item => (item.Identifier.Text, "Structure"),
            InterfaceDeclarationSyntax item => (item.Identifier.Text, "Interface"),
            EnumDeclarationSyntax item => (item.Identifier.Text, "Enum"),
            RecordDeclarationSyntax item => (item.Identifier.Text, "Record"),
            MethodDeclarationSyntax item => (item.Identifier.Text, "Méthode"),
            ConstructorDeclarationSyntax item => (item.Identifier.Text, "Constructeur"),
            PropertyDeclarationSyntax item => (item.Identifier.Text, "Propriété"),
            EventDeclarationSyntax item => (item.Identifier.Text, "Événement"),
            _ => (string.Empty, string.Empty)
        };
        if (name.Length == 0) return null;
        var lineSpan = node.GetLocation().GetLineSpan();
        return new CodeSymbol(name, kind, lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1);
    }
}
