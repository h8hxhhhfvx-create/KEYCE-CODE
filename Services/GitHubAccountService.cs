using System.ComponentModel;
using System.Diagnostics;
using KeyceCode.Models;

namespace KeyceCode.Services;

/// <summary>
/// Connexion GitHub facultative via Git Credential Manager (GCM).
/// KEYCE CODE ne reçoit ni ne stocke le mot de passe ou le jeton GitHub.
/// </summary>
public sealed class GitHubAccountService
{
    public async Task<IReadOnlyList<GitHubAccount>> ListAccountsAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["credential-manager", "github", "list"], redirectOutput: true, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error);
        return ParseAccounts(result.Output);
    }

    /// <summary>Lance l’interface officielle GCM pour ajouter un compte GitHub.</summary>
    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["credential-manager", "github", "login"], redirectOutput: false, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("La connexion GitHub a été annulée ou a échoué.");
    }

    public async Task LogoutAsync(string login, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login) || login.Any(char.IsWhiteSpace) || login.Contains('"'))
            throw new ArgumentException("Compte GitHub invalide.", nameof(login));
        var result = await RunAsync(["credential-manager", "github", "logout", login.Trim()], redirectOutput: true, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error);
    }

    internal static IReadOnlyList<GitHubAccount> ParseAccounts(string output) =>
        output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(login => new GitHubAccount(login))
            .ToArray();

    private static async Task<CommandResult> RunAsync(IReadOnlyList<string> arguments, bool redirectOutput, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                UseShellExecute = false,
                CreateNoWindow = redirectOutput,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectOutput
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        try
        {
            if (!process.Start()) throw new InvalidOperationException("Impossible de démarrer Git Credential Manager.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("Git Credential Manager est introuvable. Installez Git pour Windows puis réessayez.", ex);
        }

        if (!redirectOutput)
        {
            await process.WaitForExitAsync(cancellationToken);
            return new CommandResult(process.ExitCode, string.Empty, string.Empty);
        }

        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new CommandResult(process.ExitCode, await output, await error);
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
