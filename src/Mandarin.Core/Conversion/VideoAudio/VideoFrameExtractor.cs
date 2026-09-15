namespace Mandarin.Core.Conversion.VideoAudio;

/// <summary>Grabs a frame of a video as a PNG, for use as a preview in Crop/Redact tools.</summary>
public static class VideoFrameExtractor
{
    public static async Task<string?> ExtractFirstFrameAsync(
        IFfmpegProcessRunner ffmpegRunner,
        string videoPath,
        CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"Mandarin_frame_{Guid.NewGuid()}.png");
        
        // Fast seek to 0.25s for keyframe visual
        var arguments = new List<string> { "-y", "-ss", "00:00:00.250", "-i", videoPath, "-vframes", "1", "-pix_fmt", "rgb24", tempPath };
        var result = await ffmpegRunner.RunAsync(arguments, null, cancellationToken);

        if (!result.Success || !File.Exists(tempPath))
        {
            // Fallback to start of video without seek
            arguments = new List<string> { "-y", "-i", videoPath, "-vframes", "1", "-pix_fmt", "rgb24", tempPath };
            result = await ffmpegRunner.RunAsync(arguments, null, cancellationToken);
        }

        return result.Success && File.Exists(tempPath) ? tempPath : null;
    }

    /// <summary>
    /// Grabs <paramref name="count"/> frames spread evenly across the video, for
    /// the Trim dialog's filmstrip. Frames are small (the strip is ~52px tall) and
    /// written as PNGs in the temp folder; the caller owns deleting them.
    ///
    /// Each frame is a separate fast-seek call rather than one fps-filtered pass,
    /// because seeking is near-instant while filtering decodes the whole file.
    /// Frames that fail are simply skipped — a partial strip beats no strip.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ExtractStripAsync(
        IFfmpegProcessRunner ffmpegRunner,
        string videoPath,
        TimeSpan duration,
        int count,
        int frameHeight,
        CancellationToken cancellationToken)
    {
        var frames = new List<string>();
        if (count <= 0 || duration <= TimeSpan.Zero)
        {
            return frames;
        }

        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Sample at the middle of each slice, so the first frame isn't the
            // usual black lead-in and the last isn't past the end.
            var position = duration * ((i + 0.5) / count);
            var tempPath = Path.Combine(Path.GetTempPath(), $"Mandarin_strip_{Guid.NewGuid()}.png");

            var arguments = new List<string>
            {
                "-y",
                "-ss", position.ToString(@"hh\:mm\:ss\.fff"),
                "-i", videoPath,
                "-vframes", "1",
                "-vf", $"scale=-1:{frameHeight}",
                "-pix_fmt", "rgb24",
                tempPath,
            };

            var result = await ffmpegRunner.RunAsync(arguments, null, cancellationToken);
            if (result.Success && File.Exists(tempPath))
            {
                frames.Add(tempPath);
            }
        }

        return frames;
    }
}
