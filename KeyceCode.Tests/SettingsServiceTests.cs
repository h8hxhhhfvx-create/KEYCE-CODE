using KeyceCode.Models;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void SaveThenLoad_PreservesUserSettings()
    {
        var path = Path.Combine(Path.GetTempPath(), "KeyceCodeSettings", Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            var service = new SettingsService(path);
            service.Save(new AppSettings { Theme = "Light", EditorFontSize = 16, WordWrap = true, TabSize = 2, LearningMode = true, OllamaModel = "test-model", DebuggerPath = "tools\\vsdbg.exe" });

            var loaded = service.Load();

            Assert.Equal("Light", loaded.Theme);
            Assert.Equal(16, loaded.EditorFontSize);
            Assert.True(loaded.WordWrap);
            Assert.Equal(2, loaded.TabSize);
            Assert.True(loaded.LearningMode);
            Assert.Equal("test-model", loaded.OllamaModel);
            Assert.Equal(Path.GetFullPath("tools\\vsdbg.exe"), loaded.DebuggerPath);
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadMissingFile_ReturnsSafeDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "KeyceCodeSettings", Guid.NewGuid().ToString("N"), "settings.json");

        var settings = new SettingsService(path).Load();

        Assert.Equal("Dark", settings.Theme);
        Assert.Equal(13, settings.EditorFontSize);
        Assert.False(settings.AutoSave);
        Assert.Equal(60, settings.AutoSaveDelaySeconds);
    }

    [Fact]
    public void LoadInvalidValues_NormalizesThemToSafeDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "KeyceCodeSettings", Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{\"Theme\":\"Blue\",\"EditorFontSize\":99,\"TabSize\":3,\"OllamaEndpoint\":\"  http://server///  \",\"OllamaModel\":\"  model  \",\"LearningMode\":true,\"ExamMode\":true}");

            var settings = new SettingsService(path).Load();

            Assert.Equal("Dark", settings.Theme);
            Assert.Equal(32, settings.EditorFontSize);
            Assert.Equal(4, settings.TabSize);
            Assert.Equal("http://server", settings.OllamaEndpoint);
            Assert.Equal("model", settings.OllamaModel);
            Assert.Equal("gemini-2.5-flash", settings.GeminiModel);
            Assert.Equal("llama-3.1-8b-instant", settings.GroqModel);
            Assert.False(settings.LearningMode);
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadRecentProjects_DeduplicatesAndLimitsHistory()
    {
        var path = Path.Combine(Path.GetTempPath(), "KeyceCodeSettings", Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var projects = Enumerable.Range(0, 12).Select(index => Path.Combine(Path.GetTempPath(), $"Project-{index}")).ToList();
            projects.Add(projects[0]);
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new AppSettings { RecentProjects = projects }));

            var settings = new SettingsService(path).Load();

            Assert.Equal(10, settings.RecentProjects.Count);
            Assert.Equal(Path.GetFullPath(projects[0]), settings.RecentProjects[0]);
            Assert.Equal(settings.RecentProjects.Count, settings.RecentProjects.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadRecentProjects_IgnoresInvalidEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), "KeyceCodeSettings", Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{\"RecentProjects\":[null,\"\\u0000bad\",\"valid-project\"]}");

            var settings = new SettingsService(path).Load();

            Assert.Single(settings.RecentProjects);
            Assert.Equal(Path.GetFullPath("valid-project"), settings.RecentProjects[0]);
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadCorruptSettings_RestoresSafeDefaultsAndPreservesCorruptCopy()
    {
        var path = Path.Combine(Path.GetTempPath(), "KeyceCodeSettings", Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ invalid session json");

            var service = new SettingsService(path);
            var settings = service.Load();

            Assert.Equal("Dark", settings.Theme);
            Assert.NotNull(service.LastLoadWarning);
            Assert.Contains("corrompu", service.LastLoadWarning!, StringComparison.OrdinalIgnoreCase);
            var backups = Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "settings.json.corrupt-*.json").ToArray();
            Assert.Single(backups);
            Assert.Equal("{ invalid session json", File.ReadAllText(backups[0]));
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SaveThenLoad_PreservesSessionState()
    {
        var path = Path.Combine(Path.GetTempPath(), "KeyceCodeSettings", Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            var workspace = Path.Combine(Path.GetTempPath(), "keyce-workspace");
            var file = Path.Combine(workspace, "Program.cs");
            var service = new SettingsService(path);
            service.Save(new AppSettings { LastWorkspace = workspace, OpenFiles = [file] });

            var loaded = service.Load();

            Assert.Equal(Path.GetFullPath(workspace), loaded.LastWorkspace);
            Assert.Equal([Path.GetFullPath(file)], loaded.OpenFiles);
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
