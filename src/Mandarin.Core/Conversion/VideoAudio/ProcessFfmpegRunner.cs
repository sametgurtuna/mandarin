using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Mandarin.Core.Conversion.VideoAudio;

/// <summary>
/// Shells out to the bundled ffmpeg.exe and parses its stderr output for duration/time
/// markers to report fractional progress.
/// </summary>
public sealed partial class ProcessFfmpegRunner : IFfmpegProcessRunner
{
    private const int MaxErrorLogLines = 25;

    private readonly Func<string?>? _configuredPath;

    /// <param name="configuredPath">
    /// Supplies the user-configured ffmpeg location. It's a callback rather than a
    /// string so that pointing Mandarin at ffmpeg takes effect without restarting.
    /// </param>
    public ProcessFfmpegRunner(Func<string?>? configuredPath = null) => _configuredPath = configuredPath;

    public async Task<FfmpegRunResult> RunAsync(
        IReadOnlyList<string> arguments,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var ffmpegPath = FfmpegLocator.FindFfmpegPath(_configuredPath?.Invoke());
        if (ffmpegPath is null)
        {
            return FfmpegRunResult.Failed(
                "ffmpeg.exe was not found. Place it at 'ffmpeg\\ffmpeg.exe' next to Mandarin, " +
                "or set the MANDARIN_FFMPEG_PATH environment variable to its location.");
        }

        var startInfo = new ProcessStartInfo(ffmpegPath)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        var recentErrorLines = new List<string>();
        TimeSpan? totalDuration = null;

        void HandleErrorLine(string? line)
        {
            if (line is null)
            {
                return;
            }

            recentErrorLines.Add(line);
            if (recentErrorLines.Count > MaxErrorLogLines)
            {
                recentErrorLines.RemoveAt(0);
            }

            if (totalDuration is null)
            {
                var durationMatch = DurationRegex().Match(line);
                if (durationMatch.Success)
                {
                    totalDuration = ParseTimestamp(durationMatch);
                }
            }

            if (totalDuration is { } duration && duration > TimeSpan.Zero)
            {
                var timeMatch = TimeRegex().Match(line);
                if (timeMatch.Success)
                {
                    var elapsed = ParseTimestamp(timeMatch);
                    var fraction = elapsed.TotalSeconds / duration.TotalSeconds;
                    progress?.Report(Math.Clamp(fraction, 0, 1));
                }
            }
        }

        try
        {
            process.Start();

            var stderrTask = ReadStreamAsync(process.StandardError, HandleErrorLine, cancellationToken);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stderrTask, stdoutTask);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return FfmpegRunResult.Failed($"Could not start ffmpeg.exe: {ex.Message}");
        }

        if (process.ExitCode != 0)
        {
            var tail = string.Join(Environment.NewLine, recentErrorLines);
            return FfmpegRunResult.Failed($"ffmpeg exited with code {process.ExitCode}.{Environment.NewLine}{tail}");
        }

        progress?.Report(1);
        return FfmpegRunResult.Succeeded();
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited between the check and the kill attempt.
        }
    }

    private static async Task ReadStreamAsync(StreamReader reader, Action<string?> onLine, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            onLine(line);
        }
    }

    private static TimeSpan ParseTimestamp(Match match)
    {
        var hours = int.Parse(match.Groups[1].Value);
        var minutes = int.Parse(match.Groups[2].Value);
        var seconds = double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
        return new TimeSpan(0, hours, minutes, 0) + TimeSpan.FromSeconds(seconds);
    }

    [GeneratedRegex(@"Duration:\s*(\d+):(\d+):(\d+\.\d+)")]
    private static partial Regex DurationRegex();

    [GeneratedRegex(@"time=\s*(\d+):(\d+):(\d+\.\d+)")]
    private static partial Regex TimeRegex();
}
