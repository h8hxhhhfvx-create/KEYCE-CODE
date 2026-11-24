using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using KeyceCode.Models;

namespace KeyceCode.Services;

 
public sealed partial class CompilerService
{
    private volatile bool _busy;
    private ProcessSession? _current;

    
    public event Action<string, OutputKind>? OutputReceived;

    public bool IsBusy => _busy;

    /// <summary>Compile le projet (dotnet build) et renvoie les erreurs/avertissements trouvés.</summary>
    public Task<ExecutionResult> BuildAsync(string path, CancellationToken ct = default)
    {
        var target = Resolve(path);
        return ExecuteAsync(
            $"build {target.Argument} --nologo --verbosity minimal -tl:off",
            target.WorkingDirectory, ct);
    }

    /// <summary>Lance le programme (dotnet run). Compile d'abord si nécessaire.</summary>
    public Task<ExecutionResult> RunAsync(string path, CancellationToken ct = default)
    {
        var target = Resolve(path);

        if (target.File.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Impossible d'exécuter une solution (.sln) : choisissez un projet .csproj.");

        string args = target.IsSingleFile
            ? $"run {target.Argument}"
            : $"run --project {target.Argument} --no-launch-profile --verbosity quiet";

        return ExecuteAsync(args, target.WorkingDirectory, ct);
    }

    /// <summary>Envoie une ligne au programme en cours (Console.ReadLine, etc.).</summary>
    public void SendInput(string text) => _current?.Write(text + Environment.NewLine);

    /// <summary>Arrête le programme ou la compilation en cours.</summary>
    public void Stop() => _current?.Kill();

    // ------------------------------------------------------------------

    private async Task<ExecutionResult> ExecuteAsync(string args, string workingDirectory, CancellationToken ct)
    {
        if (_busy) throw new InvalidOperationException("Une opération est déjà en cours.");
        _busy = true;

        var captured = new StringBuilder();
        using var session = new ProcessSession("dotnet", args, workingDirectory);
        session.OutputReceived += (text, kind) =>
        {
            lock (captured)
            {
                if (captured.Length < 1_000_000) captured.Append(text);
            }
            OutputReceived?.Invoke(text, kind);
        };
        _current = session;

        try
        {
            int exitCode = await session.RunAsync(ct);
            string all;
            lock (captured) all = captured.ToString();
            return new ExecutionResult(exitCode, session.WasKilled, ParseDiagnostics(all));
        }
        catch (Win32Exception)
        {
            OutputReceived?.Invoke(
                "Impossible de lancer « dotnet ». Vérifiez que le SDK .NET est installé et présent dans le PATH.\n",
                OutputKind.Error);
            return new ExecutionResult(-1, false, Array.Empty<BuildDiagnostic>());
        }
        finally
        {
            _current = null;
            _busy = false;
        }
    }

    private static Target Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Aucun projet ou fichier sélectionné.");

        path = path.Trim().Trim('"');

        if (Directory.Exists(path))
        {
            string? project =
                Directory.EnumerateFiles(path, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault()
                ?? Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories)
                    .FirstOrDefault(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

            if (project is null)
                throw new FileNotFoundException("Aucun fichier .csproj trouvé dans ce dossier.");

            return new Target(project, Path.GetDirectoryName(project)!, false);
        }

        if (!File.Exists(path))
            throw new FileNotFoundException("Fichier introuvable : " + path);

        bool single = path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        return new Target(path, Path.GetDirectoryName(path)!, single);
    }

    private static List<BuildDiagnostic> ParseDiagnostics(string text)
    {
        var list = new List<BuildDiagnostic>();
        var seen = new HashSet<string>();

        foreach (Match m in DiagnosticRegex().Matches(text))
        {
            var d = new BuildDiagnostic(
                m.Groups["file"].Value.Trim(),
                int.Parse(m.Groups["line"].Value),
                int.Parse(m.Groups["col"].Value),
                m.Groups["sev"].Value.ToLowerInvariant(),
                m.Groups["code"].Value,
                m.Groups["msg"].Value.Trim());

            // MSBuild affiche chaque erreur deux fois (en direct + résumé) : on dédoublonne.
            if (seen.Add($"{d.File}|{d.Line}|{d.Column}|{d.Code}"))
                list.Add(d);
        }

        return list;
    }

    // Ex. : C:\proj\Program.cs(12,5): error CS1002: ; expected [C:\proj\proj.csproj]
    [GeneratedRegex(
        @"^(?<file>.+?)\((?<line>\d+),(?<col>\d+)\):\s*(?<sev>error|warning)\s+(?<code>\w+):\s*(?<msg>.*?)(?:\s+\[[^\]]+\])?\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex DiagnosticRegex();

    private sealed record Target(string File, string WorkingDirectory, bool IsSingleFile)
    {
        public string Argument => $"\"{File}\"";
    }
}
