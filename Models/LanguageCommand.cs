namespace KeyceCode.Models;

public sealed record LanguageCommand(
    string Language,
    string Executable,
    string Arguments,
    IReadOnlyList<string>? RequiredTools = null);
