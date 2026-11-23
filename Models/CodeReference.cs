namespace KeyceCode.Models;

public sealed record CodeReference(string Name, int Offset, int Length, int Line, int Column);
