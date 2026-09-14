using System.Drawing;
using PurePad.Configuration;

namespace PurePad.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"purepad-settings-{Guid.NewGuid():N}.json");

    [Fact]
    public void Missing_file_loads_defaults()
    {
        var store = new JsonSettingsStore(_path);

        AppSettings settings = store.Load();

        Assert.Equal("light", settings.ThemeId);
        Assert.True(settings.LineNumbersVisible);
        Assert.False(settings.WordWrap);
    }

    [Fact]
    public void Saved_settings_round_trip()
    {
        var store = new JsonSettingsStore(_path);
        var original = new AppSettings
        {
            ThemeId = "dark",
            FontFamily = "Consolas",
            FontSize = 12.5f,
            FontStyle = FontStyle.Bold,
            WordWrap = true,
            StatusBarVisible = true,
            LineNumbersVisible = false,
            ProblemsPanelVisible = true,
            AutoFormatOnSave = true,
            SyntaxMode = "forced",
            ForcedLanguageId = "json",
            WindowX = 100,
            WindowY = 80,
            WindowWidth = 900,
            WindowHeight = 600,
            Maximized = true,
        };

        store.Save(original);
        AppSettings loaded = new JsonSettingsStore(_path).Load();

        Assert.Equal("dark", loaded.ThemeId);
        Assert.Equal("Consolas", loaded.FontFamily);
        Assert.Equal(12.5f, loaded.FontSize);
        Assert.Equal(FontStyle.Bold, loaded.FontStyle);
        Assert.True(loaded.WordWrap);
        Assert.True(loaded.StatusBarVisible);
        Assert.False(loaded.LineNumbersVisible);
        Assert.True(loaded.ProblemsPanelVisible);
        Assert.True(loaded.AutoFormatOnSave);
        Assert.Equal("forced", loaded.SyntaxMode);
        Assert.Equal("json", loaded.ForcedLanguageId);
        Assert.Equal(900, loaded.WindowWidth);
        Assert.True(loaded.Maximized);
    }

    [Fact]
    public void Corrupt_file_loads_defaults()
    {
        File.WriteAllText(_path, "{ this is not valid json ");
        var store = new JsonSettingsStore(_path);

        AppSettings settings = store.Load();

        Assert.Equal("light", settings.ThemeId);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
