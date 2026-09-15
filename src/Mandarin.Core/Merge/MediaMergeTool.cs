using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.VideoAudio;

namespace Mandarin.Core.Merge;

/// <summary>
/// Concatenates several video or audio files, in the given order, into one, via ffmpeg's
/// concat demuxer with stream copy (fast, lossless — works reliably when all inputs share
/// the same codec/format, which merging same-extension files typically does).
/// </summary>
public sealed class MediaMergeTool : IMergeTool
{
    private readonly IFfmpegProcessRunner _ffmpegRunner;

    public MediaMergeTool(IFfmpegProcessRunner ffmpegRunner)
    {
        _ffmpegRunner = ffmpegRunner;
    }

    public IReadOnlySet<string> SupportedExtensions => MediaExtensions.AllExtensions;

    public async Task<ConversionResult> ExecuteAsync(
        IReadOnlyList<string> sourcePaths,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var listPath = Path.Combine(Path.GetTempPath(), $"Mandarin_concat_{Guid.NewGuid()}.txt");

        try
        {
            var lines = sourcePaths.Select(p => $"file '{p.Replace("'", "'\\''")}'");
            await File.WriteAllLinesAsync(listPath, lines, cancellationToken);

            var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePaths[0], "(merged)");
            var arguments = new List<string>
            {
                "-y", "-f", "concat", "-safe", "0", "-i", listPath, "-c", "copy", targetPath,
            };

            var result = await _ffmpegRunner.RunAsync(arguments, progress, cancellationToken);

            return result.Success
                ? ConversionResult.Succeeded(targetPath)
                : ConversionResult.Failed(result.ErrorMessage ?? "ffmpeg merge failed.");
        }
        finally
        {
            try
            {
                File.Delete(listPath);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }
        }
    }
}
