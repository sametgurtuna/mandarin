using Mandarin.Core.Settings;

namespace Mandarin.Core.Tests.Settings;

public class JsonSettingsServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public JsonSettingsServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MandarinTests_" + Guid.NewGuid());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Load_WhenNoFileExists_ReturnsDefaults()
    {
        var service = new JsonSettingsService(_tempDirectory);

        var settings = service.Load();

        Assert.Equal("en", settings.Language);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var service = new JsonSettingsService(_tempDirectory);
        var settings = new MandarinSettings { PanelX = 42, PanelY = 99, Language = "tr" };

        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal(42, loaded.PanelX);
        Assert.Equal(99, loaded.PanelY);
        Assert.Equal("tr", loaded.Language);
    }

    [Fact]
    public void Load_WhenFileIsCorrupt_ReturnsDefaultsInsteadOfThrowing()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(Path.Combine(_tempDirectory, "settings.json"), "{ not valid json");
        var service = new JsonSettingsService(_tempDirectory);

        var settings = service.Load();

        Assert.Equal("en", settings.Language);
    }
}
