namespace KeyceCode.Models;

public sealed record GitFileStatus(string IndexStatus, string WorkTreeStatus, string Path)
{
    public string Display => $"{IndexStatus}{WorkTreeStatus} {Path}";
}

public sealed record GitStatusSnapshot(string Branch, IReadOnlyList<GitFileStatus> Files);
public sealed record GitBranch(string Name, bool IsCurrent);
public sealed record GitCommit(string Hash, string Author, DateTimeOffset Date, string Subject);
