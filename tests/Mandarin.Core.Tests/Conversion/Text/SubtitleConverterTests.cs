using Mandarin.Core.Conversion.Text;

namespace Mandarin.Core.Tests.Conversion.Text;

public class SubtitleConverterTests : IDisposable
{
    private readonly string _tempDirectory;

    public SubtitleConverterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MandarinTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose() => Directory.Delete(_tempDirectory, recursive: true);

    [Fact]
    public async Task ConvertAsync_SrtToVtt_ProducesWebVttHeader()
    {
        var source = Path.Combine(_tempDirectory, "subs.srt");
        File.WriteAllText(source, "1\n00:00:00,000 --> 00:00:02,000\nHi\n");
        var target = Path.Combine(_tempDirectory, "subs.vtt");
        var converter = new SubtitleConverter();

        var result = await converter.ConvertAsync(source, target, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.StartsWith("WEBVTT", File.ReadAllText(target));
    }

    [Fact]
    public async Task ConvertAsync_TxtToSrt_GeneratesNumberedCues()
    {
        var source = Path.Combine(_tempDirectory, "lines.txt");
        File.WriteAllText(source, "First\nSecond\n");
        var target = Path.Combine(_tempDirectory, "lines.srt");
        var converter = new SubtitleConverter();

        var result = await converter.ConvertAsync(source, target, null, CancellationToken.None);

        Assert.True(result.Success);
        var output = File.ReadAllText(target);
        Assert.Contains("-->", output);
        Assert.Contains("First", output);
        Assert.Contains("Second", output);
    }

    [Fact]
    public async Task ConvertAsync_VttToTxt_StripsTiming()
    {
        var source = Path.Combine(_tempDirectory, "subs.vtt");
        File.WriteAllText(source, "WEBVTT\n\n00:00:00.000 --> 00:00:02.000\nHello\n");
        var target = Path.Combine(_tempDirectory, "subs.txt");
        var converter = new SubtitleConverter();

        var result = await converter.ConvertAsync(source, target, null, CancellationToken.None);

        Assert.True(result.Success);
        var output = File.ReadAllText(target);
        Assert.DoesNotContain("-->", output);
        Assert.Contains("Hello", output);
    }
}
