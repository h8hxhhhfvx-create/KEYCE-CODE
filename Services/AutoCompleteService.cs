using System.Text.RegularExpressions;

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

    public IReadOnlyList<string> GetSuggestions(string source, int offset)
    {
        offset = Math.Clamp(offset, 0, source.Length);
        var start = offset;
        while (start > 0 && (char.IsLetterOrDigit(source[start - 1]) || source[start - 1] == '_')) start--;
        var prefix = source[start..offset];
        var identifiers = Regex.Matches(source, @"\b[A-Za-z_][A-Za-z0-9_]*\b")
            .Select(match => match.Value);

        return Keywords.Concat(CommonMembers).Concat(identifiers)
            .Where(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value.Equals(prefix, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(value => value)
            .Take(80)
            .ToArray();
    }
}
