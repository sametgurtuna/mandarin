using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.VideoAudio;

namespace Mandarin.Core.AdvancedTools.VideoAudio;

/// <summary>Splits a video/audio file into two parts at a chosen timestamp.</summary>
public sealed class VideoAudioSplitTool : IAdvancedTool
{
    private readonly IFfmpegProcessRunner _ffmpegRunner;

    public VideoAudioSplitTool(IFfmpegProcessRunner ffmpegRunner)
    {
        _ffmpegRunner = ffmpegRunner;
    }

    public AdvancedToolKind Kind => AdvancedToolKind.Split;

    public IReadOnlySet<string> SupportedExtensions => MediaExtensions.AllExtensions;

    public async Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (options is not MediaSplitOptions split || split.At <= TimeSpan.Zero)
        {
            return ConversionResult.Failed("No split point was chosen.");
        }

        var part1Path = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SplitSuffix(1));
        var part2Path = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SplitSuffix(2));

        var firstHalfProgress = new Progress<double>(f => progress?.Report(f * 0.5));
        var part1Result = await _ffmpegRunner.RunAsync(
            new List<string> { "-y", "-i", sourcePath, "-to", FfmpegTime.Format(split.At), "-c", "copy", part1Path },
            firstHalfProgress,
            cancellationToken);

        if (!part1Result.Success)
        {
            return ConversionResult.Failed(part1Result.ErrorMessage ?? "ffmpeg split (part 1) failed.");
        }

        var secondHalfProgress = new Progress<double>(f => progress?.Report(0.5 + (f * 0.5)));
        var part2Result = await _ffmpegRunner.RunAsync(
            new List<string> { "-y", "-i", sourcePath, "-ss", FfmpegTime.Format(split.At), "-c", "copy", part2Path },
            secondHalfProgress,
            cancellationToken);

        if (!part2Result.Success)
        {
            return ConversionResult.Failed(part2Result.ErrorMessage ?? "ffmpeg split (part 2) failed.");
        }

        return ConversionResult.Succeeded(part1Path);
    }
}
