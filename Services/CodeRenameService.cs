using KeyceCode.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace KeyceCode.Services;

public sealed class CodeRenameService
{
    public RenameResult? Rename(string source, int offset, string newName)
    {
        var text = source ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newName) || !SyntaxFacts.IsValidIdentifier(newName))
            throw new ArgumentException("Le nouveau nom n’est pas un identifiant C# valide.", nameof(newName));
        var tree = CSharpSyntaxTree.ParseText(text);
        var root = tree.GetRoot();
        var token = root.FindToken(Math.Clamp(offset, 0, text.Length));
        if (!token.IsKind(SyntaxKind.IdentifierToken) || string.IsNullOrWhiteSpace(token.ValueText)) return null;
        var oldName = token.ValueText;
        if (string.Equals(oldName, newName, StringComparison.Ordinal)) return new RenameResult(text, oldName, newName, 0);
        var identifiers = root.DescendantTokens().Where(item => item.IsKind(SyntaxKind.IdentifierToken) && item.ValueText == oldName).ToArray();
        var updatedRoot = root.ReplaceTokens(identifiers, (original, _) => SyntaxFactory.Identifier(original.LeadingTrivia, newName, original.TrailingTrivia));
        return new RenameResult(updatedRoot.ToFullString(), oldName, newName, identifiers.Length);
    }
}
