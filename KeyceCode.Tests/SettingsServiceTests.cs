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
            service.Save(new AppSettings { Theme = "Light", EditorFontSize = 16, WordWrap = true, TabSize = 2, LearningMode = true, OllamaModel = "test-model" });

            var loaded = service.Load();

            Assert.Equal("Light", loaded.Theme);
            Assert.Equal(16, loaded.EditorFontSize);
            Assert.True(loaded.WordWrap);
            Assert.Equal(2, loaded.TabSize);
            Assert.True(loaded.LearningMode);
            Assert.Equal("test-model", loaded.OllamaModel);
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
}
