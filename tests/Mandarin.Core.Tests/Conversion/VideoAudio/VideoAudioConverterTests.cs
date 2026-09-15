using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.VideoAudio;

namespace Mandarin.Core.Tests.Conversion.VideoAudio;

public class VideoAudioConverterTests
{
    private sealed class FakeFfmpegProcessRunner : IFfmpegProcessRunner
    {
        public List<string>? LastArguments { get; private set; }
        public FfmpegRunResult ResultToReturn { get; set; } = FfmpegRunResult.Succeeded();

        public Task<FfmpegRunResult> RunAsync(
            IReadOnlyList<string> arguments,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            LastArguments = arguments.ToList();
            progress?.Report(1);
            return Task.FromResult(ResultToReturn);
        }
    }

    [Fact]
    public void CanConvert_VideoToAudio_IsAllowed()
    {
        var converter = new VideoAudioConverter(new FakeFfmpegProcessRunner());

        Assert.True(converter.CanConvert("mp4", "mp3"));
    }

    [Fact]
    public void CanConvert_AudioToVideo_IsNotAllowed()
    {
        var converter = new VideoAudioConverter(new FakeFfmpegProcessRunner());

        Assert.False(converter.CanConvert("mp3", "mp4"));
    }

    [Fact]
    public void CanConvert_AudioToAudio_IsAllowed()
    {
        var converter = new VideoAudioConverter(new FakeFfmpegProcessRunner());

        Assert.True(converter.CanConvert("wav", "flac"));
    }

    [Fact]
    public async Task ConvertAsync_AudioTarget_AddsVnFlagToStripVideo()
    {
        var runner = new FakeFfmpegProcessRunner();
        var converter = new VideoAudioConverter(runner);

        await converter.ConvertAsync(@"C:\videos\clip.mp4", @"C:\videos\clip.mp3", null, CancellationToken.None);

        Assert.Contains("-vn", runner.LastArguments!);
    }

    [Fact]
    public async Task ConvertAsync_VideoTarget_DoesNotAddVnFlag()
    {
        var runner = new FakeFfmpegProcessRunner();
        var converter = new VideoAudioConverter(runner);

        await converter.ConvertAsync(@"C:\videos\clip.mov", @"C:\videos\clip.mp4", null, CancellationToken.None);

        Assert.DoesNotContain("-vn", runner.LastArguments!);
    }

    [Fact]
    public async Task ConvertAsync_WhenFfmpegFails_ReturnsFailureResult()
    {
        var runner = new FakeFfmpegProcessRunner { ResultToReturn = FfmpegRunResult.Failed("boom") };
        var converter = new VideoAudioConverter(runner);

        var result = await converter.ConvertAsync(@"C:\videos\clip.mp4", @"C:\videos\clip.mp3", null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("boom", result.ErrorMessage);
    }

    [Fact]
    public void Registry_IncludesVideoAudioConverter_ForMp4Targets()
    {
        var registry = new ConverterRegistry(new IConverter[] { new VideoAudioConverter(new FakeFfmpegProcessRunner()) });

        var targets = registry.GetAvailableTargets("mp4");

        Assert.Contains("mp3", targets);
        Assert.Contains("webm", targets);
        Assert.DoesNotContain("mp4", targets);
    }

    [Fact]
    public void Registry_ForAudioSource_DoesNotOfferVideoTargets()
    {
        var registry = new ConverterRegistry(new IConverter[] { new VideoAudioConverter(new FakeFfmpegProcessRunner()) });

        var targets = registry.GetAvailableTargets("wav");

        Assert.Contains("flac", targets);
        Assert.DoesNotContain("mp4", targets);
        Assert.DoesNotContain("mov", targets);
    }
}
