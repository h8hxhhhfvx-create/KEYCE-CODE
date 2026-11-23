namespace KeyceCode.Models;

public sealed record CodeSymbol(string Name, string Kind, int Line, int Column)
{
    public string Display => $"{Kind} {Name} — ligne {Line}";
}
