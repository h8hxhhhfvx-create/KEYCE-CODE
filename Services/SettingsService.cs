using System.Text.Json;
using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class SettingsService
{
    private readonly string settingsPath;

    public SettingsService(string? settingsPath = null)
    {
        this.settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeyceCode",
            "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(settingsPath)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath));
            return Normalize(settings ?? new AppSettings());
        }
        catch (JsonException) { return new AppSettings(); }
        catch (IOException) { return new AppSettings(); }
        catch (UnauthorizedAccessException) { return new AppSettings(); }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(settingsPath)
            ?? throw new IOException("Le dossier des paramètres est introuvable.");
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(Normalize(settings), new JsonSerializerOptions { WriteIndented = true });
        var temporaryPath = settingsPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, settingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.Theme = string.Equals(settings.Theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
        settings.EditorFontSize = Math.Clamp(settings.EditorFontSize, 8, 32);
        settings.TabSize = settings.TabSize is 2 or 4 or 8 ? settings.TabSize : 4;
        settings.OllamaEndpoint = string.IsNullOrWhiteSpace(settings.OllamaEndpoint)
            ? "http://localhost:11434"
            : settings.OllamaEndpoint.Trim().TrimEnd('/');
        settings.OllamaModel = string.IsNullOrWhiteSpace(settings.OllamaModel) ? "qwen2.5:7b" : settings.OllamaModel.Trim();
        settings.GeminiModel = string.IsNullOrWhiteSpace(settings.GeminiModel) ? "gemini-2.5-flash" : settings.GeminiModel.Trim();
        settings.GroqModel = string.IsNullOrWhiteSpace(settings.GroqModel) ? "llama-3.1-8b-instant" : settings.GroqModel.Trim();
        settings.DebuggerPath = string.IsNullOrWhiteSpace(settings.DebuggerPath)
            ? null
            : TryGetFullPath(settings.DebuggerPath.Trim().Trim('"'));
        settings.RecentProjects = (settings.RecentProjects ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(TryGetFullPath)
            .Where(path => path is not null)
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
        settings.LastWorkspace = string.IsNullOrWhiteSpace(settings.LastWorkspace) ? null : TryGetFullPath(settings.LastWorkspace);
        settings.OpenFiles = (settings.OpenFiles ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(TryGetFullPath)
            .Where(path => path is not null)
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
        if (settings.ExamMode) settings.LearningMode = false;
        return settings;
    }

    private static string? TryGetFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }
}
