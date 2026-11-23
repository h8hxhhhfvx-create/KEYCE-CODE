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
    public Task UnstageAsync(IEnumerable<string> paths, CancellationToken ct = default) => RunCheckedAsync(["restore", "--staged", "--", .. paths], ct);

    public async Task<string> GetDiffAsync(string? path = null, bool staged = false, CancellationToken ct = default)
    {
        List<string> args = staged ? ["diff", "--cached", "--no-ext-diff"] : ["diff", "--no-ext-diff"];
        if (!string.IsNullOrWhiteSpace(path)) args.AddRange(["--", path]);
        var result = await RunAsync(args, ct);
        if (result.ExitCode != 0) throw new InvalidOperationException(result.StdErr.Trim());
        return result.StdOut;
    }

    public async Task<IReadOnlyList<GitCommit>> GetHistoryAsync(int limit = 100, CancellationToken ct = default)
    {
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
        var format = "%H%x1f%an%x1f%aI%x1f%s%x1e";
        var result = await RunAsync(["log", $"-n{limit}", $"--format={format}"], ct);
        if (result.ExitCode != 0) throw new InvalidOperationException(result.StdErr.Trim());

        var commits = new List<GitCommit>();
        foreach (var record in result.StdOut.Split('\x1e', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = record.Trim('\r', '\n').Split('\x1f');
            if (fields.Length < 4 || !DateTimeOffset.TryParse(fields[2], out var date)) continue;
            commits.Add(new GitCommit(fields[0], fields[1], date, fields[3]));
        }
        return commits;
    }

    public Task CommitAsync(string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Le message du commit est vide.", nameof(message));
        return RunCheckedAsync(["commit", "-m", message], ct);
    }
    public Task PullAsync(CancellationToken ct = default) => RunCheckedAsync(["pull", "--ff-only"], ct);
    public Task PushAsync(CancellationToken ct = default) => RunCheckedAsync(["push"], ct);
    public Task FetchAsync(string? remote = null, CancellationToken ct = default) =>
        RunCheckedAsync(string.IsNullOrWhiteSpace(remote) ? ["fetch", "--prune"] : ["fetch", "--prune", remote.Trim()], ct);

    public Task StashPushAsync(string? message = null, CancellationToken ct = default)
    {
        var args = new List<string> { "stash", "push", "--include-untracked" };
        if (!string.IsNullOrWhiteSpace(message)) args.AddRange(["-m", message.Trim()]);
        return RunCheckedAsync(args, ct);
    }

    public Task StashPopAsync(CancellationToken ct = default) => RunCheckedAsync(["stash", "pop"], ct);

    public Task MergeAsync(string branch, CancellationToken ct = default)
    {
        ValidateBranchName(branch);
        return RunCheckedAsync(["merge", "--no-edit", branch.Trim()], ct);
    }

    public Task RebaseAsync(string branch, CancellationToken ct = default)
    {
        ValidateBranchName(branch);
        return RunCheckedAsync(["rebase", branch.Trim()], ct);
    }

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
        if (string.IsNullOrWhiteSpace(name) || !string.Equals(name, name.Trim(), StringComparison.Ordinal) ||
            name.StartsWith('-') || name.StartsWith('/') || name.EndsWith('/') || name.EndsWith('.') ||
            name.Contains("..", StringComparison.Ordinal) || name.Contains("//", StringComparison.Ordinal) ||
            name.Contains("@{", StringComparison.Ordinal) || name == "@" ||
            name.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) ||
                character is '~' or '^' or ':' or '?' or '*' or '[' or '\\'))
            throw new ArgumentException("Nom de branche Git invalide.", nameof(name));
    }

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
