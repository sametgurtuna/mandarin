using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.VideoAudio;

namespace Mandarin.Core.AdvancedTools.VideoAudio;

/// <summary>Cuts a video/audio file down to the [Start, End] range chosen by the user.</summary>
public sealed class VideoAudioTrimTool : IAdvancedTool
{
    private readonly IFfmpegProcessRunner _ffmpegRunner;

    public VideoAudioTrimTool(IFfmpegProcessRunner ffmpegRunner)
    {
        _ffmpegRunner = ffmpegRunner;
    }

    public AdvancedToolKind Kind => AdvancedToolKind.Trim;

    public IReadOnlySet<string> SupportedExtensions => MediaExtensions.AllExtensions;

    public async Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (options is not TrimOptions trim || trim.End <= trim.Start)
        {
            return ConversionResult.Failed("Invalid trim range: the end must be after the start.");
        }

        var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SuffixFor(AdvancedToolKind.Trim));
        var arguments = new List<string>
        {
            "-y", "-i", sourcePath,
            "-ss", FfmpegTime.Format(trim.Start),
            "-to", FfmpegTime.Format(trim.End),
            "-c", "copy",
            targetPath,
        };

        var result = await _ffmpegRunner.RunAsync(arguments, progress, cancellationToken);

        return result.Success
            ? ConversionResult.Succeeded(targetPath)
            : ConversionResult.Failed(result.ErrorMessage ?? "ffmpeg trim failed.");
    }
}
