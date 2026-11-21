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
            return settings ?? new AppSettings();
        }
        catch (JsonException) { return new AppSettings(); }
        catch (IOException) { return new AppSettings(); }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(settingsPath)
            ?? throw new IOException("Le dossier des paramètres est introuvable.");
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsPath, json);
    }
}
