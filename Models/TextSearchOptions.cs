namespace KeyceCode.Models;

public sealed record TextSearchOptions(bool MatchCase = false, bool UseRegularExpression = false, bool WholeWord = false);

public readonly record struct TextMatch(int Index, int Length);

public sealed record TextReplaceResult(string Text, int Count);
