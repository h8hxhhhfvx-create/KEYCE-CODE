using KeyceCode.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace KeyceCode.Services;

public sealed class CodeReferenceService
{
    public IReadOnlyList<CodeReference> FindReferences(string source, int offset)
    {
        source ??= string.Empty;
        offset = Math.Clamp(offset, 0, source.Length);
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var token = root.FindToken(offset);
        if (!token.IsKind(SyntaxKind.IdentifierToken) && offset > 0)
            token = root.FindToken(offset - 1);
        if (!token.IsKind(SyntaxKind.IdentifierToken) || string.IsNullOrWhiteSpace(token.ValueText)) return [];

        return root.DescendantTokens()
            .Where(item => item.IsKind(SyntaxKind.IdentifierToken) && item.ValueText == token.ValueText)
            .Select(item =>
            {
                var position = item.GetLocation().GetLineSpan().StartLinePosition;
                return new CodeReference(item.ValueText, item.Span.Start, item.Span.Length, position.Line + 1, position.Character + 1);
            })
            .OrderBy(item => item.Offset)
            .ToArray();
    }
}
