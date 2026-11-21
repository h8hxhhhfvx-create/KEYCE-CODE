namespace KeyceCode.Models;

/// <summary>Une erreur ou un avertissement de compilation (ex. Program.cs(12,5): error CS1002).</summary>
public sealed record BuildDiagnostic(
    string File,
    int Line,
    int Column,
    string Severity,
    string Code,
    string Message)
{
    public bool IsError => Severity == "error";

    public string Display =>
        $"{(IsError ? "✘" : "⚠")} {Code} — {Path.GetFileName(File)}({Line},{Column}) : {Message}";
}

public sealed record ExecutionResult(
    int ExitCode,
    bool WasStopped,
    IReadOnlyList<BuildDiagnostic> Diagnostics)
{
    public bool Success => ExitCode == 0 && !WasStopped;
}
