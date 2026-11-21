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
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(settingsPath)
            ?? throw new IOException("Le dossier des paramètres est introuvable.");
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(Normalize(settings), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsPath, json);
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
        settings.RecentProjects = settings.RecentProjects
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
        if (settings.ExamMode) settings.LearningMode = false;
        return settings;
    }
}
