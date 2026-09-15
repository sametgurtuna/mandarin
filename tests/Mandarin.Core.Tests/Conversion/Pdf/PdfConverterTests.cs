using Mandarin.Core.Conversion.Pdf;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace Mandarin.Core.Tests.Conversion.Pdf;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class PdfConverterTests : IDisposable
{
    private readonly string _tempDirectory;

    public PdfConverterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MandarinTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose() => Directory.Delete(_tempDirectory, recursive: true);

    private string CreateTestPdf(int pageCount = 1)
    {
        var path = Path.Combine(_tempDirectory, "doc.pdf");
        using var document = new PdfDocument();
        var font = new XFont("Verdana", 14, XFontStyle.Regular);

        for (var i = 0; i < pageCount; i++)
        {
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawString($"Hello page {i + 1}", font, XBrushes.Black, new XPoint(40, 60));
        }

        document.Save(path);
        return path;
    }

    [Fact]
    public async Task ConvertAsync_PdfToTxt_ExtractsText()
    {
        var source = CreateTestPdf();
        var target = Path.Combine(_tempDirectory, "doc.txt");
        var converter = new PdfConverter();

        var result = await converter.ConvertAsync(source, target, null, CancellationToken.None);

        Assert.True(result.Success);
        var text = File.ReadAllText(target);
        Assert.Contains("Hello page 1", text);
    }

    [Fact]
    public async Task ConvertAsync_PdfToDocx_WritesNonEmptyDocx()
    {
        var source = CreateTestPdf();
        var target = Path.Combine(_tempDirectory, "doc.docx");
        var converter = new PdfConverter();

        var result = await converter.ConvertAsync(source, target, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(new FileInfo(target).Length > 0);
    }

    [Fact]
    public async Task ConvertAsync_SinglePagePdfToPng_WritesExactlyOneImageAtRequestedPath()
    {
        var source = CreateTestPdf(pageCount: 1);
        var target = Path.Combine(_tempDirectory, "doc.png");
        var converter = new PdfConverter();

        var result = await converter.ConvertAsync(source, target, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(target, result.OutputPath);
        Assert.True(File.Exists(target));
    }

    [Fact]
    public async Task ConvertAsync_MultiPagePdfToJpg_WritesOneImagePerPage()
    {
        var source = CreateTestPdf(pageCount: 3);
        var target = Path.Combine(_tempDirectory, "doc.jpg");
        var converter = new PdfConverter();

        var result = await converter.ConvertAsync(source, target, null, CancellationToken.None);

        Assert.True(result.Success);
        var producedFiles = Directory.GetFiles(_tempDirectory, "*.jpg");
        Assert.Equal(3, producedFiles.Length);
    }
}
