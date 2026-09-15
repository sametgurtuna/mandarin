using ImageMagick;
using Mandarin.Core.AdvancedTools;
using Mandarin.Core.AdvancedTools.Images;

namespace Mandarin.Core.Tests.AdvancedTools.Images;

public class ImageToolsTests : IDisposable
{
    private readonly string _tempDirectory;

    public ImageToolsTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MandarinTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose() => Directory.Delete(_tempDirectory, recursive: true);

    private string CreateTestPng(int width = 40, int height = 30)
    {
        var path = Path.Combine(_tempDirectory, "source.png");
        using var image = new MagickImage(MagickColors.SteelBlue, (uint)width, (uint)height);
        image.Write(path);
        return path;
    }

    [Fact]
    public async Task Compress_ProducesSuffixedFile()
    {
        var source = CreateTestPng();
        var tool = new ImageCompressTool();

        var result = await tool.ExecuteAsync(source, new CompressOptions(30), null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("source (compressed).png", Path.GetFileName(result.OutputPath));
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public async Task Crop_ProducesImageWithRequestedDimensions()
    {
        var source = CreateTestPng(width: 40, height: 30);
        var tool = new ImageCropTool();

        var result = await tool.ExecuteAsync(source, new CropOptions(5, 5, 10, 8), null, CancellationToken.None);

        Assert.True(result.Success);
        using var cropped = new MagickImage(result.OutputPath!);
        Assert.Equal(10u, cropped.Width);
        Assert.Equal(8u, cropped.Height);
    }

    [Fact]
    public async Task Crop_WithoutOptions_Fails()
    {
        var source = CreateTestPng();
        var tool = new ImageCropTool();

        var result = await tool.ExecuteAsync(source, null, null, CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task StripMetadata_ProducesSuffixedFile()
    {
        var source = CreateTestPng();
        var tool = new ImageStripMetadataTool();

        var result = await tool.ExecuteAsync(source, null, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public async Task EditPhoto_AppliesAdjustmentsAndSaves()
    {
        var source = CreateTestPng();
        var tool = new ImageEditTool();
        var options = new ImageEditOptions(
            Exposure: 0.5,
            Brightness: 10,
            Contrast: 15,
            Gamma: 1.1,
            Saturation: 20,
            Hue: 5,
            Clarity: 10,
            NoiseReduction: 5
        );

        var result = await tool.ExecuteAsync(source, options, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(result.OutputPath));
        Assert.Equal("source (edited).png", Path.GetFileName(result.OutputPath));
    }

    [Fact]
    public async Task EditMetadata_ReadsAndModifies()
    {
        var source = CreateTestPng();
        var tags = ImageMetadataTool.ReadTags(source);
        Assert.NotEmpty(tags);

        var tool = new ImageMetadataTool();
        var result = await tool.ExecuteAsync(source, new MetadataEditOptions(RemoveAll: true), null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public async Task RedactPhoto_AppliesSolidAndBlurRedactions()
    {
        var source = CreateTestPng(100, 100);
        var tool = new ImageRedactTool();
        var regions = new List<RedactionRegion>
        {
            new(0.1, 0.1, 0.3, 0.3, RedactStyle.Solid),
            new(0.5, 0.5, 0.4, 0.4, RedactStyle.Blur),
            new(0.1, 0.6, 0.3, 0.3, RedactStyle.Pixelate)
        };

        var result = await tool.ExecuteAsync(source, new RedactOptions(regions), null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(result.OutputPath));
        Assert.Equal("source (redacted).png", Path.GetFileName(result.OutputPath));
    }
}
