namespace KeyceCode.Models;

public sealed class CodeFile
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public bool IsDirectory { get; init; }
    public List<CodeFile> Children { get; } = [];
}
