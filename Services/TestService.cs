using System.Diagnostics;
using System.Text.RegularExpressions;
using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed partial class TestService
{
    public string ResolveTestProject(string path) => ResolveTarget(path);

    public async Task<TestRunResult> RunAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        var target = ResolveTarget(projectPath);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet", $"test {Quote(target)} --nologo --no-restore --verbosity minimal")
            {
                WorkingDirectory = Path.GetDirectoryName(target)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        if (!process.Start()) throw new InvalidOperationException("Impossible de démarrer dotnet test.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = (await stdout) + (await stderr);
        var summary = SummaryRegex().Match(output);
        var passed = summary.Success ? int.Parse(summary.Groups["passed"].Value) : 0;
        var failed = summary.Success ? int.Parse(summary.Groups["failed"].Value) : (process.ExitCode == 0 ? 0 : 1);
        var skipped = summary.Success ? int.Parse(summary.Groups["skipped"].Value) : 0;
        return new TestRunResult(process.ExitCode, process.ExitCode == 0 && failed == 0, output, passed, failed, skipped);
    }

    private static string ResolveTarget(string path)
    {
        var full = Path.GetFullPath(path.Trim().Trim('"'));
        if (File.Exists(full) && full.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) return full;
        if (Directory.Exists(full))
        {
            var project = Directory.EnumerateFiles(full, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault()
                ?? Directory.EnumerateFiles(full, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
            if (project is not null) return project;
        }
        throw new FileNotFoundException("Aucun projet .csproj trouvé pour les tests.", full);
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    [GeneratedRegex(@"Passed:\s*(?<passed>\d+),\s*Failed:\s*(?<failed>\d+),\s*Skipped:\s*(?<skipped>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SummaryRegex();
}
