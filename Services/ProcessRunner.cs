using System.Diagnostics;

namespace KeyceCode.Services;

internal static class ProcessRunner
{
    public sealed record Result(int ExitCode, string Output);

    public static async Task<Result> RunAsync(string executable, string arguments, string workingDirectory, CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable, arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        if (!process.Start()) throw new InvalidOperationException($"Impossible de démarrer {executable}.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new Result(process.ExitCode, (await stdout) + (await stderr));
    }
}
