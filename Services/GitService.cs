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

    public async Task<IReadOnlyList<GitBranch>> GetBranchesAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["branch", "--format=%(HEAD)|%(refname:short)"], cancellationToken);
        if (result.ExitCode != 0) throw new InvalidOperationException(result.StdErr.Trim());
        return result.StdOut.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('|', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => new GitBranch(parts[1].Trim(), parts[0] == "*"))
            .ToArray();
    }

    public Task CreateBranchAsync(string name, CancellationToken ct = default)
    {
        ValidateBranchName(name);
        return RunCheckedAsync(["switch", "-c", name.Trim()], ct);
    }

    public Task CheckoutAsync(string name, CancellationToken ct = default)
    {
        ValidateBranchName(name);
        return RunCheckedAsync(["switch", name.Trim()], ct);
    }

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

    private static void ValidateBranchName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsWhiteSpace) || name.StartsWith('-'))
            throw new ArgumentException("Nom de branche Git invalide.", nameof(name));
    }

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
