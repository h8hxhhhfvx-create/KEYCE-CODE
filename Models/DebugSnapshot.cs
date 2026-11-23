namespace KeyceCode.Models;

public sealed record DebugFrame(string Name, string? FilePath, int Line, int Column, int Id);
public sealed record DebugVariable(string Name, string Value, string? Type);
public sealed record DebugSnapshot(int ThreadId, IReadOnlyList<DebugFrame> Frames, IReadOnlyList<DebugVariable> Variables);
