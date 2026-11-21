namespace KeyceCode.Models;

public sealed record TestRunResult(int ExitCode, bool Success, string Output, int Passed, int Failed, int Skipped);
