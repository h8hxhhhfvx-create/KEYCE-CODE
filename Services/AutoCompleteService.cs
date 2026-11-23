using System.Text.RegularExpressions;
using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class AutoCompleteService
{
    private static readonly string[] Keywords =
    [
        "abstract", "async", "await", "bool", "break", "case", "catch", "class", "const",
        "continue", "decimal", "default", "delegate", "else", "enum", "event", "explicit",
        "extern", "false", "finally", "fixed", "for", "foreach", "if", "implicit", "in",
        "interface", "internal", "is", "lock", "namespace", "new", "null", "object", "operator",
        "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return",
        "sealed", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
        "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile",
        "while", "var", "record", "init", "required", "get", "set", "value", "with", "yield"
    ];

    private static readonly string[] CommonMembers =
    [
        "Console", "WriteLine", "Write", "ReadLine", "Length", "ToString", "Equals", "GetHashCode",
        "String", "Math", "DateTime", "Task", "List", "Dictionary", "Count", "Add", "Remove", "Contains"
    ];

    private static readonly CompletionSuggestion[] Snippets =
    [
        new("cw — Console.WriteLine", "Console.WriteLine();", "Snippet"),
        new("for — boucle for", "for (int i = 0; i < length; i++)\n{\n    \n}", "Snippet"),
        new("if — condition", "if (condition)\n{\n    \n}", "Snippet"),
        new("class — classe C#", "class ClassName\n{\n    \n}", "Snippet")
    ];

    public IReadOnlyList<string> GetSuggestions(string source, int offset)
        => GetCompletionItems(source, offset).Select(item => item.DisplayText).ToArray();

    public IReadOnlyList<CompletionSuggestion> GetCompletionItems(string source, int offset)
    {
        source ??= string.Empty;
        offset = Math.Clamp(offset, 0, source.Length);
        var start = offset;
        while (start > 0 && (char.IsLetterOrDigit(source[start - 1]) || source[start - 1] == '_')) start--;
        var prefix = source[start..offset];
        var identifiers = Regex.Matches(source, @"\b[A-Za-z_][A-Za-z0-9_]*\b")
            .Select(match => match.Value);

        var words = Keywords.Concat(CommonMembers).Concat(identifiers)
            .Select(value => new CompletionSuggestion(value, value, "Suggestion"));
        return words.Concat(Snippets)
            .Where(item => item.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.DisplayText, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.DisplayText.Equals(prefix, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(item => item.Kind == "Snippet" ? 0 : 1)
            .ThenBy(item => item.DisplayText)
            .Take(80)
            .ToArray();
    }
}
