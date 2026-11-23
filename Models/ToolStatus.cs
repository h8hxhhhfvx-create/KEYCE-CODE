namespace KeyceCode.Models;

public sealed record ToolStatus(
    string Executable,
    bool IsAvailable,
    string? ResolvedPath,
    string? Version,
    string Message);
