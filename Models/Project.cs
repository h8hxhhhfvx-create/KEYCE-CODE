namespace KeyceCode.Models;

public sealed class Project
{
    public required string Name { get; init; }
    public required string RootPath { get; init; }
    public string? EntryFilePath { get; set; }
    public List<CodeFile> Files { get; } = [];
}
