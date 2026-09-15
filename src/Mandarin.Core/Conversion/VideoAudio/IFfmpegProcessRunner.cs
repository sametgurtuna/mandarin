namespace Mandarin.Core.Conversion.VideoAudio;

/// <summary>
/// Isolates the ffmpeg.exe process invocation behind an interface, so the binary call is
/// easy to swap or mock in tests without shelling out to a real process.
/// </summary>
public interface IFfmpegProcessRunner
{
    Task<FfmpegRunResult> RunAsync(
        IReadOnlyList<string> arguments,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}
