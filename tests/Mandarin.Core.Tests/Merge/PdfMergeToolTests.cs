using Mandarin.Core.Merge;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace Mandarin.Core.Tests.Merge;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class PdfMergeToolTests : IDisposable
{
    private readonly string _tempDirectory;

    public PdfMergeToolTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MandarinTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose() => Directory.Delete(_tempDirectory, recursive: true);

    private string CreateTestPdf(string name, int pageCount)
    {
        var path = Path.Combine(_tempDirectory, name);
        using var document = new PdfDocument();
        var font = new XFont("Verdana", 14, XFontStyle.Regular);

        for (var i = 0; i < pageCount; i++)
        {
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawString($"{name} page {i + 1}", font, XBrushes.Black, new XPoint(40, 60));
        }

        document.Save(path);
        return path;
    }

    [Fact]
    public async Task ExecuteAsync_CombinesAllPagesInOrder()
    {
        var a = CreateTestPdf("a.pdf", 2);
        var b = CreateTestPdf("b.pdf", 3);
        var tool = new PdfMergeTool();

        var result = await tool.ExecuteAsync(new[] { a, b }, null, CancellationToken.None);

        Assert.True(result.Success);
        using var merged = PdfReader.Open(result.OutputPath!, PdfDocumentOpenMode.InformationOnly);
        Assert.Equal(5, merged.PageCount);
    }
}
