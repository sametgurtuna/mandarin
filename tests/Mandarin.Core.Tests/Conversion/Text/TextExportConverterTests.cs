using Mandarin.Core.Conversion.Text;

namespace Mandarin.Core.Tests.Conversion.Text;

public class TextExportConverterTests : IDisposable
{
    private readonly string _tempDirectory;

    public TextExportConverterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MandarinTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose() => Directory.Delete(_tempDirectory, recursive: true);

    private string CreateTestTxt(string content = "Hello Mandarin, this is a test file with a reasonably long line of text to force wrapping across a few words.")
    {
        var path = Path.Combine(_tempDirectory, "notes.txt");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task ConvertAsync_TxtToPdf_WritesNonEmptyPdf()
    {
        var source = CreateTestTxt();
        var target = Path.Combine(_tempDirectory, "notes.pdf");
        var converter = new TextExportConverter();

        var result = await converter.ConvertAsync(source, target, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(new FileInfo(target).Length > 0);
    }

    [Fact]
    public async Task ConvertAsync_TxtToPng_WritesNonEmptyImage()
    {
        var source = CreateTestTxt();
        var target = Path.Combine(_tempDirectory, "notes.png");
        var converter = new TextExportConverter();

        var result = await converter.ConvertAsync(source, target, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(new FileInfo(target).Length > 0);
    }

    [Fact]
    public async Task ConvertAsync_LongText_ProducesMultiPagePdfWithoutError()
    {
        var longText = string.Join("\n", Enumerable.Repeat("This is a repeated line to force pagination in the generated PDF document.", 200));
        var source = CreateTestTxt(longText);
        var target = Path.Combine(_tempDirectory, "notes.pdf");
        var converter = new TextExportConverter();

        var result = await converter.ConvertAsync(source, target, null, CancellationToken.None);

        Assert.True(result.Success);
    }
}
