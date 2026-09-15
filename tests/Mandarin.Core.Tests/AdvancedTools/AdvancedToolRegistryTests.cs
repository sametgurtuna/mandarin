using Mandarin.Core.AdvancedTools;
using Mandarin.Core.AdvancedTools.Images;
using Mandarin.Core.AdvancedTools.Pdf;

namespace Mandarin.Core.Tests.AdvancedTools;

public class AdvancedToolRegistryTests
{
    private static AdvancedToolRegistry CreateRegistry() => new(new IAdvancedTool[]
    {
        new ImageCompressTool(),
        new ImageCropTool(),
        new ImageStripMetadataTool(),
        new PdfSplitTool(),
        new PdfStripMetadataTool(),
    });

    [Fact]
    public void GetAvailableTools_ForJpg_ReturnsImageTools()
    {
        var registry = CreateRegistry();

        var tools = registry.GetAvailableTools("jpg");

        Assert.Contains(AdvancedToolKind.Compress, tools);
        Assert.Contains(AdvancedToolKind.Crop, tools);
        Assert.Contains(AdvancedToolKind.StripMetadata, tools);
        Assert.DoesNotContain(AdvancedToolKind.Trim, tools);
        Assert.DoesNotContain(AdvancedToolKind.Split, tools);
    }

    [Fact]
    public void GetAvailableTools_ForPdf_ReturnsOnlySplitAndStripMetadata()
    {
        var registry = CreateRegistry();

        var tools = registry.GetAvailableTools("pdf");

        Assert.Contains(AdvancedToolKind.Split, tools);
        Assert.Contains(AdvancedToolKind.StripMetadata, tools);
        Assert.DoesNotContain(AdvancedToolKind.Crop, tools);
    }

    [Fact]
    public void FindTool_ForUnsupportedCombination_ReturnsNull()
    {
        var registry = CreateRegistry();

        Assert.Null(registry.FindTool("pdf", AdvancedToolKind.Crop));
    }
}
