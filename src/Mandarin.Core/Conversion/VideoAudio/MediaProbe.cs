using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Mandarin.Core.Conversion.VideoAudio;

/// <summary>
/// Reads duration and video dimensions from ffmpeg's own diagnostic output (running
/// `ffmpeg -i file` with no output always prints this to stderr before exiting non-zero,
/// so no separate ffprobe binary is needed). Used by the Trim/Split/Crop tool dialogs to
/// show the user what range or frame size they're working with.
/// </summary>
public static partial class MediaProbe
{
    /// <summary>
    /// Static entry point for the user-configured ffmpeg location, so probing
    /// honours it the same way conversions do. Set once at startup.
    /// </summary>
    public static Func<string?>? ConfiguredFfmpegPath { get; set; }

    public static async Task<MediaInfo?> ProbeAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var ffmpegPath = FfmpegLocator.FindFfmpegPath(ConfiguredFfmpegPath?.Invoke());
        if (ffmpegPath is null)
        {
            return null;
        }

        var startInfo = new ProcessStartInfo(ffmpegPath)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(sourcePath);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stderr = await stderrTask;
            await stdoutTask;

            return Parse(stderr);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static MediaInfo Parse(string ffmpegOutput)
    {
        TimeSpan? duration = null;
        int? width = null;
        int? height = null;

        var durationMatch = DurationRegex().Match(ffmpegOutput);
        if (durationMatch.Success)
        {
            var hours = int.Parse(durationMatch.Groups[1].Value);
            var minutes = int.Parse(durationMatch.Groups[2].Value);
            var seconds = double.Parse(durationMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            duration = new TimeSpan(0, hours, minutes, 0) + TimeSpan.FromSeconds(seconds);
        }

        var videoMatch = VideoDimensionsRegex().Match(ffmpegOutput);
        if (videoMatch.Success)
        {
            width = int.Parse(videoMatch.Groups[1].Value);
            height = int.Parse(videoMatch.Groups[2].Value);
        }

        return new MediaInfo(duration, width, height);
    }

    [GeneratedRegex(@"Duration:\s*(\d+):(\d+):(\d+\.\d+)")]
    private static partial Regex DurationRegex();

    [GeneratedRegex(@"Video:.*?(\d{2,5})x(\d{2,5})")]
    private static partial Regex VideoDimensionsRegex();
}
