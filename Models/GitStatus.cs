namespace KeyceCode.Models;

public sealed record GitFileStatus(string IndexStatus, string WorkTreeStatus, string Path)
{
    public string Display => $"{IndexStatus}{WorkTreeStatus} {Path}";
}

public sealed record GitStatusSnapshot(string Branch, IReadOnlyList<GitFileStatus> Files);
