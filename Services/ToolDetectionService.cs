using System.Diagnostics;
using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class ToolDetectionService
{
    public ToolStatus Inspect(string executable)
    {
        var path = ResolveOnPath(executable);
        if (path is null)
            return new ToolStatus(executable, false, null, null,
                $"Outil « {executable} » introuvable dans le PATH.");

        string? version = null;
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Arguments = "--version",
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (process is not null && process.WaitForExit(1500))
            {
                var output = process.StandardOutput.ReadToEnd();
                if (string.IsNullOrWhiteSpace(output)) output = process.StandardError.ReadToEnd();
                version = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            }
        }
        catch
        {
            // La présence dans le PATH reste une information utile même si l’outil
            // ne supporte pas --version.
        }

        return new ToolStatus(executable, true, path, version,
            version is null ? $"Outil « {executable} » disponible." : $"{executable} : {version}");
    }

    private static string? ResolveOnPath(string executable)
    {
        if (Path.IsPathRooted(executable) && File.Exists(executable)) return executable;
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), executable);
            if (File.Exists(candidate)) return candidate;
            foreach (var extension in extensions)
            {
                candidate = Path.Combine(directory.Trim(), executable + extension);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }
}
