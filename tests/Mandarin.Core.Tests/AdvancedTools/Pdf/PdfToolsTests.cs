using Mandarin.Core.AdvancedTools;
using Mandarin.Core.AdvancedTools.Pdf;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace Mandarin.Core.Tests.AdvancedTools.Pdf;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class PdfToolsTests : IDisposable
{
    private readonly string _tempDirectory;

    public PdfToolsTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MandarinTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose() => Directory.Delete(_tempDirectory, recursive: true);

    private string CreateTestPdf(int pageCount)
    {
        var path = Path.Combine(_tempDirectory, "doc.pdf");
        using var document = new PdfDocument();
        var font = new XFont("Verdana", 14, XFontStyle.Regular);

        for (var i = 0; i < pageCount; i++)
        {
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawString($"Page {i + 1}", font, XBrushes.Black, new XPoint(40, 60));
        }

        document.Save(path);
        return path;
    }

    [Fact]
    public async Task Split_ExtractsRequestedPageRange()
    {
        var source = CreateTestPdf(5);
        var tool = new PdfSplitTool();

        var result = await tool.ExecuteAsync(source, new PdfSplitOptions(2, 4), null, CancellationToken.None);

        Assert.True(result.Success);
        using var document = PdfSharpCore.Pdf.IO.PdfReader.Open(result.OutputPath!, PdfDocumentOpenMode.InformationOnly);
        Assert.Equal(3, document.PageCount);
    }

    [Fact]
    public async Task Split_RangeBeyondPageCount_Fails()
    {
        var source = CreateTestPdf(2);
        var tool = new PdfSplitTool();

        var result = await tool.ExecuteAsync(source, new PdfSplitOptions(5, 6), null, CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task StripMetadata_ClearsInfoFields()
    {
        var source = CreateTestPdf(1);
        using (var document = PdfSharpCore.Pdf.IO.PdfReader.Open(source, PdfDocumentOpenMode.Modify))
        {
            document.Info.Title = "Secret Title";
            document.Info.Author = "Someone";
            document.Save(source);
        }

        var tool = new PdfStripMetadataTool();
        var result = await tool.ExecuteAsync(source, null, null, CancellationToken.None);

        Assert.True(result.Success);
        using var stripped = PdfSharpCore.Pdf.IO.PdfReader.Open(result.OutputPath!, PdfDocumentOpenMode.InformationOnly);
        Assert.True(string.IsNullOrEmpty(stripped.Info.Title));
        Assert.True(string.IsNullOrEmpty(stripped.Info.Author));
    }
}
