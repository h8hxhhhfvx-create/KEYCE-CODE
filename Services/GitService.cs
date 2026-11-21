using System.Diagnostics;
using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class GitService
{
    private readonly string workingDirectory;

    public GitService(string workingDirectory) => this.workingDirectory = Path.GetFullPath(workingDirectory);

    public async Task<GitStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var branch = (await RunAsync(["branch", "--show-current"], cancellationToken)).StdOut.Trim();
        var result = await RunAsync(["status", "--porcelain=v1"], cancellationToken);
        if (result.ExitCode != 0) throw new InvalidOperationException(result.StdErr.Trim());
        var files = result.StdOut.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).Select(ParseStatusLine).ToList();
        return new GitStatusSnapshot(string.IsNullOrWhiteSpace(branch) ? "(sans branche)" : branch, files);
    }

    public Task StageAsync(IEnumerable<string> paths, CancellationToken ct = default) => RunCheckedAsync(["add", "--", .. paths], ct);
    public Task CommitAsync(string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Le message du commit est vide.", nameof(message));
        return RunCheckedAsync(["commit", "-m", message], ct);
    }
    public Task PullAsync(CancellationToken ct = default) => RunCheckedAsync(["pull", "--ff-only"], ct);
    public Task PushAsync(CancellationToken ct = default) => RunCheckedAsync(["push"], ct);

    private async Task RunCheckedAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var result = await RunAsync(args, ct);
        if (result.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr);
    }

    private async Task<ProcessResult> RunAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git", WorkingDirectory = workingDirectory, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            }
        };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        if (!process.Start()) throw new InvalidOperationException("Impossible de démarrer Git.");
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static GitFileStatus ParseStatusLine(string line) => line.Length < 4
        ? new("?", "?", line.Trim())
        : new(line[0].ToString(), line[1].ToString(), line[3..].Trim('"'));

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
