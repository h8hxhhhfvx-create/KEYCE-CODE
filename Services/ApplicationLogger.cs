using System.Text.Json;

namespace KeyceCode.Services;

public interface IApplicationLogger
{
    void Info(string message, string? operation = null);
    void Warning(string message, string? operation = null);
    void Error(Exception exception, string operation);
}

/// <summary>Journal local JSONL, sans dépendance à l’interface graphique.</summary>
public sealed class ApplicationLogger : IApplicationLogger
{
    public const long MaximumLogBytes = 2 * 1024 * 1024;
    private readonly object sync = new();
    private readonly string logPath;

    public ApplicationLogger(string? logPath = null)
    {
        this.logPath = logPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KEYCE CODE", "logs", "keyce-code.jsonl");
    }

    public void Info(string message, string? operation = null) => Write("Information", message, operation, null);
    public void Warning(string message, string? operation = null) => Write("Warning", message, operation, null);
    public void Error(Exception exception, string operation) => Write("Error", exception.Message, operation, exception.ToString());

    private void Write(string level, string message, string? operation, string? details)
    {
        var entry = new
        {
            timestamp = DateTimeOffset.UtcNow,
            level,
            operation,
            message,
            details
        };
        var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
        lock (sync)
        {
            try
            {
                var directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                RotateIfNeeded();
                File.AppendAllText(logPath, line);
            }
            catch (IOException)
            {
                // Une panne du journal ne doit jamais faire tomber l’application.
            }
            catch (UnauthorizedAccessException)
            {
                // Même règle pour un profil Windows sans accès au dossier de logs.
            }
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(logPath) || new FileInfo(logPath).Length < MaximumLogBytes) return;
        var archive = logPath + ".old";
        try
        {
            File.Move(logPath, archive, overwrite: true);
        }
        catch (IOException)
        {
            // Le fichier courant reste utilisable si une autre instance le verrouille.
        }
    }
}
