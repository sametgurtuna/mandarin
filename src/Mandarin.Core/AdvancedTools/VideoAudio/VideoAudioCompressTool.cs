using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.VideoAudio;

namespace Mandarin.Core.AdvancedTools.VideoAudio;

/// <summary>Re-encodes video/audio at a lower bitrate — optionally to a target file size — to shrink it.</summary>
public sealed class VideoAudioCompressTool : IAdvancedTool
{
    /// <summary>Muxing overhead and bitrate wobble; aim slightly under the target.</summary>
    private const double TargetSizeSafety = 0.95;

    /// <summary>
    /// Bitrate floor for a typical source. It stops a tiny target size from
    /// producing a smeared, unwatchable file — but it is capped against the
    /// source's own bitrate below, because a floor above what the source already
    /// uses would make perfectly reachable targets impossible.
    /// </summary>
    private const int DefaultMinVideoBitrateKbps = 120;

    private const int AbsoluteMinVideoBitrateKbps = 16;
    private const int MinAudioBitrateKbps = 48;

    private readonly IFfmpegProcessRunner _ffmpegRunner;

    public VideoAudioCompressTool(IFfmpegProcessRunner ffmpegRunner)
    {
        _ffmpegRunner = ffmpegRunner;
    }

    public AdvancedToolKind Kind => AdvancedToolKind.Compress;

    public IReadOnlySet<string> SupportedExtensions => MediaExtensions.AllExtensions;

    public async Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var settings = options as CompressOptions ?? new CompressOptions(50);
        var quality = Math.Clamp(settings.Quality, 1, 100);
        var sourceExtension = FileExtensions.GetNormalizedExtension(sourcePath);
        var isVideo = MediaExtensions.VideoExtensions.Contains(sourceExtension);
        var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SuffixFor(AdvancedToolKind.Compress));

        var audioBitrateKbps = settings.AudioBitrateKbps is { } fixedAudio && fixedAudio > 0
            ? Math.Max(MinAudioBitrateKbps, fixedAudio)
            : 64 + (int)(quality / 100.0 * (256 - 64));

        int videoBitrateKbps = isVideo ? 400 + (int)(quality / 100.0 * (6000 - 400)) : 0;
        var minVideoKbps = DefaultMinVideoBitrateKbps;

        // A target size is a bitrate budget: size / duration, minus what audio takes.
        if (settings.TargetSizeBytes is { } targetBytes && targetBytes > 0)
        {
            var info = await MediaProbe.ProbeAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            if (info?.Duration is { TotalSeconds: > 0.5 } duration)
            {
                var totalKbps = targetBytes * 8.0 * TargetSizeSafety / duration.TotalSeconds / 1000.0;

                // Never insist on a floor above what the source itself uses: an
                // already-light clip can go lower without turning to mush.
                var sourceKbps = SourceBitrateKbps(sourcePath, duration);
                if (sourceKbps > 0)
                {
                    minVideoKbps = Math.Clamp((int)(sourceKbps * 0.35), AbsoluteMinVideoBitrateKbps, DefaultMinVideoBitrateKbps);
                }

                if (isVideo)
                {
                    audioBitrateKbps = Math.Clamp(audioBitrateKbps, MinAudioBitrateKbps, Math.Max(MinAudioBitrateKbps, (int)(totalKbps * 0.2)));
                    videoBitrateKbps = Math.Max(minVideoKbps, (int)(totalKbps - audioBitrateKbps));
                }
                else
                {
                    audioBitrateKbps = Math.Max(MinAudioBitrateKbps, (int)totalKbps);
                }
            }
            else
            {
                // Without a duration there is no budget to compute; the quality
                // setting still applies, so this degrades rather than failing.
                return await RunAsync(sourcePath, targetPath, isVideo, videoBitrateKbps, audioBitrateKbps, settings, progress, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var result = await RunAsync(sourcePath, targetPath, isVideo, videoBitrateKbps, audioBitrateKbps, settings, progress, cancellationToken)
            .ConfigureAwait(false);

        if (settings.TargetSizeBytes is { } wanted && wanted > 0 && result.Success && result.OutputPath is not null)
        {
            result = await CorrectOvershootAsync(
                sourcePath, result, wanted, isVideo, videoBitrateKbps, audioBitrateKbps, minVideoKbps, settings, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Bitrate targeting lands near the ask, not on it. If the first pass came out
    /// too big, re-encode once with the bitrate scaled by how far off it was. If
    /// the budget is already at the floor — where more shrinking would mean an
    /// unwatchable file — keep the output and say so rather than pretending.
    /// </summary>
    private async Task<ConversionResult> CorrectOvershootAsync(
        string sourcePath,
        ConversionResult first,
        long wanted,
        bool isVideo,
        int videoBitrateKbps,
        int audioBitrateKbps,
        int minVideoKbps,
        CompressOptions settings,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var actual = new FileInfo(first.OutputPath!).Length;
        if (actual <= wanted)
        {
            return first;
        }

        var atFloor = isVideo
            ? videoBitrateKbps <= minVideoKbps
            : audioBitrateKbps <= MinAudioBitrateKbps;

        if (!atFloor)
        {
            var ratio = wanted / (double)actual;
            var retryVideo = Math.Max(minVideoKbps, (int)(videoBitrateKbps * ratio));
            var retryAudio = isVideo ? audioBitrateKbps : Math.Max(MinAudioBitrateKbps, (int)(audioBitrateKbps * ratio));

            var retryPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SuffixFor(AdvancedToolKind.Compress));
            var retry = await RunAsync(sourcePath, retryPath, isVideo, retryVideo, retryAudio, settings, progress, cancellationToken)
                .ConfigureAwait(false);

            if (retry.Success && retry.OutputPath is not null)
            {
                var retrySize = new FileInfo(retry.OutputPath).Length;
                TryDelete(first.OutputPath!);

                return retrySize <= wanted
                    ? retry
                    : ConversionResult.SucceededWithWarning(retry.OutputPath, DescribeOvershoot(retrySize, wanted));
            }

            TryDelete(retryPath);
        }

        return ConversionResult.SucceededWithWarning(first.OutputPath!, DescribeOvershoot(actual, wanted));
    }

    /// <summary>Average bitrate of the source, from its size on disk.</summary>
    private static double SourceBitrateKbps(string sourcePath, TimeSpan duration)
    {
        try
        {
            return new FileInfo(sourcePath).Length * 8.0 / duration.TotalSeconds / 1000.0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static string DescribeOvershoot(long actual, long wanted) =>
        $"Couldn't get under {Megabytes(wanted)} MB without ruining it — the result is {Megabytes(actual)} MB. " +
        "Try a lower resolution.";

    private static string Megabytes(long bytes) => (bytes / 1024.0 / 1024.0).ToString("0.0");

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover attempt is harmless next to losing the user's result.
        }
    }

    private async Task<ConversionResult> RunAsync(
        string sourcePath,
        string targetPath,
        bool isVideo,
        int videoBitrateKbps,
        int audioBitrateKbps,
        CompressOptions settings,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "-y", "-i", sourcePath };

        if (isVideo)
        {
            arguments.Add("-b:v");
            arguments.Add($"{videoBitrateKbps}k");

            if (settings.MaxVideoHeight is { } maxHeight && maxHeight > 0)
            {
                // -2 keeps the aspect ratio and an even width, which H.264 requires;
                // min() means we never upscale a smaller source.
                arguments.Add("-vf");
                arguments.Add($"scale=-2:min({maxHeight}\\,ih)");
            }
        }

        arguments.Add("-b:a");
        arguments.Add($"{audioBitrateKbps}k");
        arguments.Add(targetPath);

        var result = await _ffmpegRunner.RunAsync(arguments, progress, cancellationToken).ConfigureAwait(false);

        return result.Success
            ? ConversionResult.Succeeded(targetPath)
            : ConversionResult.Failed(result.ErrorMessage ?? "ffmpeg compression failed.");
    }
}
