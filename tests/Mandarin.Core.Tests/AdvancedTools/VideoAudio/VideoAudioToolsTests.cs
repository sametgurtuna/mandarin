using Mandarin.Core.AdvancedTools;
using Mandarin.Core.AdvancedTools.VideoAudio;
using Mandarin.Core.Conversion.VideoAudio;

namespace Mandarin.Core.Tests.AdvancedTools.VideoAudio;

public class VideoAudioToolsTests
{
    private sealed class FakeFfmpegProcessRunner : IFfmpegProcessRunner
    {
        public List<string>? LastArguments { get; private set; }
        public int CallCount { get; private set; }
        public FfmpegRunResult ResultToReturn { get; set; } = FfmpegRunResult.Succeeded();

        public Task<FfmpegRunResult> RunAsync(
            IReadOnlyList<string> arguments,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastArguments = arguments.ToList();
            progress?.Report(1);
            return Task.FromResult(ResultToReturn);
        }
    }

    [Fact]
    public async Task Compress_VideoSource_SetsVideoAndAudioBitrate()
    {
        var runner = new FakeFfmpegProcessRunner();
        var tool = new VideoAudioCompressTool(runner);

        var result = await tool.ExecuteAsync(@"C:\videos\clip.mp4", new CompressOptions(50), null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("-b:v", runner.LastArguments!);
        Assert.Contains("-b:a", runner.LastArguments!);
        Assert.EndsWith("(compressed).mp4", result.OutputPath);
    }

    [Fact]
    public async Task Compress_AudioSource_OnlySetsAudioBitrate()
    {
        var runner = new FakeFfmpegProcessRunner();
        var tool = new VideoAudioCompressTool(runner);

        await tool.ExecuteAsync(@"C:\audio\song.mp3", new CompressOptions(50), null, CancellationToken.None);

        Assert.DoesNotContain("-b:v", runner.LastArguments!);
        Assert.Contains("-b:a", runner.LastArguments!);
    }

    [Fact]
    public async Task Crop_BuildsCropFilterFromOptions()
    {
        var runner = new FakeFfmpegProcessRunner();
        var tool = new VideoCropTool(runner);

        await tool.ExecuteAsync(@"C:\videos\clip.mp4", new CropOptions(10, 20, 300, 200), null, CancellationToken.None);

        var filterIndex = runner.LastArguments!.IndexOf("-vf");
        Assert.True(filterIndex >= 0);
        Assert.Equal("crop=300:200:10:20", runner.LastArguments![filterIndex + 1]);
    }

    [Fact]
    public async Task Trim_InvalidRange_FailsWithoutCallingFfmpeg()
    {
        var runner = new FakeFfmpegProcessRunner();
        var tool = new VideoAudioTrimTool(runner);

        var result = await tool.ExecuteAsync(
            @"C:\videos\clip.mp4",
            new TrimOptions(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5)),
            null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task Trim_ValidRange_PassesSsAndToFlags()
    {
        var runner = new FakeFfmpegProcessRunner();
        var tool = new VideoAudioTrimTool(runner);

        await tool.ExecuteAsync(
            @"C:\videos\clip.mp4",
            new TrimOptions(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)),
            null,
            CancellationToken.None);

        Assert.Contains("-ss", runner.LastArguments!);
        Assert.Contains("-to", runner.LastArguments!);
    }

    [Fact]
    public async Task Split_RunsFfmpegTwiceAndProducesTwoOutputNames()
    {
        var runner = new FakeFfmpegProcessRunner();
        var tool = new VideoAudioSplitTool(runner);

        var result = await tool.ExecuteAsync(
            @"C:\videos\clip.mp4",
            new MediaSplitOptions(TimeSpan.FromSeconds(30)),
            null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, runner.CallCount);
        Assert.EndsWith("(part 1).mp4", result.OutputPath);
    }

    [Fact]
    public async Task RedactVideo_PassesDrawboxOrDelogoFilter()
    {
        var runner = new FakeFfmpegProcessRunner();
        var tool = new VideoRedactTool(runner);
        var regions = new List<RedactionRegion>
        {
            new(0.1, 0.2, 0.3, 0.4, RedactStyle.Solid),
            new(0.5, 0.5, 0.2, 0.2, RedactStyle.Blur)
        };

        await tool.ExecuteAsync(@"C:\videos\clip.mp4", new RedactOptions(regions), null, CancellationToken.None);

        var filterIndex = runner.LastArguments!.IndexOf("-vf");
        Assert.True(filterIndex >= 0);
        var vf = runner.LastArguments![filterIndex + 1];
        Assert.Contains("drawbox", vf);
        Assert.Contains("delogo", vf);
    }
}
