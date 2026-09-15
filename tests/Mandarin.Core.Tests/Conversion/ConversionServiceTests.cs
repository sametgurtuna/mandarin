using ImageMagick;
using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.Images;
using Mandarin.Core.Settings;

namespace Mandarin.Core.Tests.Conversion;

public class ConversionServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public ConversionServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MandarinTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose() => Directory.Delete(_tempDirectory, recursive: true);

    private static ConverterRegistry CreateRegistry() =>
        new(new IConverter[] { new MagickImageConverter(), new ImageToDocxConverter() });

    private string CreateTestPng()
    {
        var path = Path.Combine(_tempDirectory, "source.png");
        using var image = new MagickImage(MagickColors.OrangeRed, 8, 8);
        image.Write(path);
        return path;
    }

    [Fact]
    public async Task ConvertAsync_PngToJpg_WritesJpgAndRemembersLastUsedFormat()
    {
        var sourcePath = CreateTestPng();
        var settingsService = new JsonSettingsService(_tempDirectory);
        var settings = settingsService.Load();
        var service = new ConversionService(CreateRegistry(), settingsService, settings);

        var result = await service.ConvertAsync(sourcePath, "jpg", progress: null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(result.OutputPath));
        Assert.Equal("jpg", settingsService.Load().LastUsedFormatsByFileType["png"]);
    }

    [Fact]
    public void GetSuggestedTarget_WithNoHistory_ReturnsFirstAvailableTarget()
    {
        var sourcePath = Path.Combine(_tempDirectory, "source.png");
        var settingsService = new JsonSettingsService(_tempDirectory);
        var settings = settingsService.Load();
        var service = new ConversionService(CreateRegistry(), settingsService, settings);

        var suggested = service.GetSuggestedTarget(sourcePath);

        Assert.NotNull(suggested);
    }

    [Fact]
    public async Task GetSuggestedTarget_AfterAConversion_ReturnsLastUsedFormat()
    {
        var sourcePath = CreateTestPng();
        var settingsService = new JsonSettingsService(_tempDirectory);
        var settings = settingsService.Load();
        var service = new ConversionService(CreateRegistry(), settingsService, settings);

        await service.ConvertAsync(sourcePath, "webp", progress: null, CancellationToken.None);
        var suggested = service.GetSuggestedTarget(sourcePath);

        Assert.Equal("webp", suggested);
    }

    [Fact]
    public async Task ConvertAsync_UnsupportedTarget_FailsWithoutThrowing()
    {
        var sourcePath = CreateTestPng();
        var settingsService = new JsonSettingsService(_tempDirectory);
        var settings = settingsService.Load();
        var service = new ConversionService(CreateRegistry(), settingsService, settings);

        var result = await service.ConvertAsync(sourcePath, "mp3", progress: null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }
}
