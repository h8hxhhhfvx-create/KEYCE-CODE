namespace KeyceCode.Models;

public sealed record SearchResult(string FilePath, int Line, int Column, string Text)
{
    public string Display => $"{Path.GetFileName(FilePath)}:{Line}:{Column}  {Text.Trim()}";
}
