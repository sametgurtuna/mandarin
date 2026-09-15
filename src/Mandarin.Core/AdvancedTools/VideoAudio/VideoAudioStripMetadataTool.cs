using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.VideoAudio;

namespace Mandarin.Core.AdvancedTools.VideoAudio;

/// <summary>Strips embedded metadata (tags, GPS, author, etc.) from video/audio.</summary>
public sealed class VideoAudioStripMetadataTool : IAdvancedTool
{
    private readonly IFfmpegProcessRunner _ffmpegRunner;

    public VideoAudioStripMetadataTool(IFfmpegProcessRunner ffmpegRunner)
    {
        _ffmpegRunner = ffmpegRunner;
    }

    public AdvancedToolKind Kind => AdvancedToolKind.StripMetadata;

    public IReadOnlySet<string> SupportedExtensions => MediaExtensions.AllExtensions;

    public async Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SuffixFor(AdvancedToolKind.StripMetadata));
        var arguments = new List<string>
        {
            "-y", "-i", sourcePath,
            "-map_metadata", "-1",
            "-c", "copy",
            targetPath,
        };

        var result = await _ffmpegRunner.RunAsync(arguments, progress, cancellationToken);

        return result.Success
            ? ConversionResult.Succeeded(targetPath)
            : ConversionResult.Failed(result.ErrorMessage ?? "ffmpeg metadata strip failed.");
    }
}
