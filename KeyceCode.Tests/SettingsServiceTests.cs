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
            service.Save(new AppSettings { Theme = "Light", EditorFontSize = 16, WordWrap = true, TabSize = 2 });

            var loaded = service.Load();

            Assert.Equal("Light", loaded.Theme);
            Assert.Equal(16, loaded.EditorFontSize);
            Assert.True(loaded.WordWrap);
            Assert.Equal(2, loaded.TabSize);
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
}
