using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.Images;

namespace Mandarin.Core.Tests.Conversion;

public class ConverterRegistryTests
{
    private static ConverterRegistry CreateRegistry() =>
        new(new IConverter[] { new MagickImageConverter(), new ImageToDocxConverter() });

    [Fact]
    public void IsSourceSupported_ForKnownImageExtension_ReturnsTrue()
    {
        var registry = CreateRegistry();

        Assert.True(registry.IsSourceSupported("jpg"));
        Assert.False(registry.IsSourceSupported("mp4"));
    }

    [Fact]
    public void GetAvailableTargets_ForJpg_IncludesPngAndDocxButNotSelf()
    {
        var registry = CreateRegistry();

        var targets = registry.GetAvailableTargets("jpg");

        Assert.Contains("png", targets);
        Assert.Contains("docx", targets);
        Assert.Contains("pdf", targets);
        Assert.DoesNotContain("jpg", targets);
    }

    [Fact]
    public void FindConverter_ForUnsupportedPair_ReturnsNull()
    {
        var registry = CreateRegistry();

        var converter = registry.FindConverter("mp4", "mp3");

        Assert.Null(converter);
    }
}
