using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.VideoAudio;

namespace Mandarin.Core.AdvancedTools.VideoAudio;

/// <summary>Crops every frame of a video to a pixel rectangle chosen by the user.</summary>
public sealed class VideoCropTool : IAdvancedTool
{
    private readonly IFfmpegProcessRunner _ffmpegRunner;

    public VideoCropTool(IFfmpegProcessRunner ffmpegRunner)
    {
        _ffmpegRunner = ffmpegRunner;
    }

    public AdvancedToolKind Kind => AdvancedToolKind.Crop;

    public IReadOnlySet<string> SupportedExtensions => MediaExtensions.VideoExtensions;

    public async Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (options is not CropOptions crop || crop.Width <= 0 || crop.Height <= 0)
        {
            return ConversionResult.Failed("No crop rectangle was selected.");
        }

        int width = Math.Max(2, crop.Width - (crop.Width % 2));
        int height = Math.Max(2, crop.Height - (crop.Height % 2));
        int x = Math.Max(0, crop.X - (crop.X % 2));
        int y = Math.Max(0, crop.Y - (crop.Y % 2));

        var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SuffixFor(AdvancedToolKind.Crop));
        var arguments = new List<string>
        {
            "-y",
            "-i", sourcePath,
            "-vf", $"crop={width}:{height}:{x}:{y}",
            "-c:v", "libx264",
            "-pix_fmt", "yuv420p",
            "-c:a", "copy",
            targetPath,
        };

        var result = await _ffmpegRunner.RunAsync(arguments, progress, cancellationToken);
        if (!result.Success || !File.Exists(targetPath))
        {
            // Fallback without libx264 flags
            arguments = new List<string>
            {
                "-y",
                "-i", sourcePath,
                "-vf", $"crop={width}:{height}:{x}:{y}",
                "-c:a", "copy",
                targetPath,
            };
            result = await _ffmpegRunner.RunAsync(arguments, progress, cancellationToken);
        }

        return result.Success && File.Exists(targetPath)
            ? ConversionResult.Succeeded(targetPath)
            : ConversionResult.Failed(result.ErrorMessage ?? "ffmpeg crop failed.");
    }
}
