using System.Text.RegularExpressions;
using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class TextSearchService
{
    public IReadOnlyList<TextMatch> Find(string source, string query, TextSearchOptions? options = null)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(query)) return [];
        var regex = CreateRegex(query, options ?? new TextSearchOptions());
        return regex.Matches(source).Select(match => new TextMatch(match.Index, match.Length)).ToArray();
    }

    public TextReplaceResult ReplaceAll(string source, string query, string replacement, TextSearchOptions? options = null)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(query)) return new TextReplaceResult(source, 0);
        var regex = CreateRegex(query, options ?? new TextSearchOptions());
        var matches = regex.Matches(source);
        return new TextReplaceResult(regex.Replace(source, replacement ?? string.Empty), matches.Count);
    }

    private static Regex CreateRegex(string query, TextSearchOptions options)
    {
        var pattern = options.UseRegularExpression ? query : Regex.Escape(query);
        if (options.WholeWord) pattern = $"\\b(?:{pattern})\\b";
        var flags = RegexOptions.CultureInvariant;
        if (!options.MatchCase) flags |= RegexOptions.IgnoreCase;
        try { return new Regex(pattern, flags); }
        catch (ArgumentException ex) { throw new ArgumentException("L’expression régulière est invalide.", nameof(query), ex); }
    }
}
