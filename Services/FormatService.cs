using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace KeyceCode.Services;

public sealed class FormatService
{
    public string FormatCSharp(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return source;

        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().NormalizeWhitespace(indentation: "    ", eol: Environment.NewLine).ToFullString();
    }
}
