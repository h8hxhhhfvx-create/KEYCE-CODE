using System.Text.RegularExpressions;

namespace KeyceCode.Models;

public readonly record struct AnsiConsoleSegment(string Text, OutputKind Kind);

/// <summary>Interprète les séquences ANSI courantes produites par les outils CLI.</summary>
public static partial class AnsiConsoleParser
{
    [GeneratedRegex("\\x1B\\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex EscapeSequenceRegex();

    public static IReadOnlyList<AnsiConsoleSegment> Parse(string text, OutputKind defaultKind)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<AnsiConsoleSegment>();

        var segments = new List<AnsiConsoleSegment>();
        var current = defaultKind;
        var cursor = 0;
        foreach (Match match in EscapeSequenceRegex().Matches(text))
        {
            AddText(segments, text[cursor..match.Index], current);
            if (match.Value.EndsWith('m'))
                current = ApplySgr(match.Value, defaultKind, current);
            cursor = match.Index + match.Length;
        }

        AddText(segments, text[cursor..], current);
        return segments;
    }

    private static void AddText(List<AnsiConsoleSegment> segments, string text, OutputKind kind)
    {
        if (text.Length == 0) return;
        if (segments.Count > 0 && segments[^1].Kind == kind)
            segments[^1] = segments[^1] with { Text = segments[^1].Text + text };
        else
            segments.Add(new AnsiConsoleSegment(text, kind));
    }

    private static OutputKind ApplySgr(string sequence, OutputKind defaultKind, OutputKind current)
    {
        var parameters = sequence[2..^1];
        if (parameters.Length == 0) return defaultKind;

        foreach (var value in parameters.Split(';'))
        {
            if (!int.TryParse(value, out var code)) continue;
            current = code switch
            {
                0 => defaultKind,
                31 or 91 => OutputKind.Error,
                32 or 92 => OutputKind.Success,
                33 or 93 or 34 or 94 or 36 or 96 => OutputKind.Info,
                37 or 97 => defaultKind,
                _ => current
            };
        }

        return current;
    }
}
