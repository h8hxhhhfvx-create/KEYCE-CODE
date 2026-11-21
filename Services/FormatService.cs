using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace KeyceCode.Services;

public sealed class FormatService
{
    public string FormatCSharp(string source, int indentationSize = 4)
    {
        if (string.IsNullOrWhiteSpace(source)) return source;

        var tree = CSharpSyntaxTree.ParseText(source);
        var indentation = new string(' ', Math.Clamp(indentationSize, 1, 16));
        return tree.GetRoot().NormalizeWhitespace(indentation: indentation, eol: Environment.NewLine).ToFullString();
    }
}
