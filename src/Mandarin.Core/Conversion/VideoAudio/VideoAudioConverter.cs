namespace Mandarin.Core.Conversion.VideoAudio;

/// <summary>
/// Converts between video and audio formats by shelling out to ffmpeg. Also covers video
/// -> audio extraction (e.g. MP4 -> MP3), since that's just a video source with an audio
/// target extension.
/// </summary>
public sealed class VideoAudioConverter : IConverter
{
    private readonly IFfmpegProcessRunner _ffmpegRunner;

    public VideoAudioConverter(IFfmpegProcessRunner ffmpegRunner)
    {
        _ffmpegRunner = ffmpegRunner;
    }

    public IReadOnlySet<string> SourceExtensions { get; } = MediaExtensions.AllExtensions;

    public IReadOnlySet<string> TargetExtensions { get; } = MediaExtensions.AllExtensions;

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        if (!SourceExtensions.Contains(sourceExtension) || !TargetExtensions.Contains(targetExtension))
        {
            return false;
        }

        // An audio file has no video stream to convert into a video container.
        if (MediaExtensions.AudioExtensions.Contains(sourceExtension) && MediaExtensions.VideoExtensions.Contains(targetExtension))
        {
            return false;
        }

        return true;
    }

    public async Task<ConversionResult> ConvertAsync(
        string sourcePath,
        string targetPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var targetExtension = FileExtensions.GetNormalizedExtension(targetPath);

        var arguments = new List<string> { "-y", "-i", sourcePath };
        if (MediaExtensions.AudioExtensions.Contains(targetExtension))
        {
            arguments.Add("-vn");
        }

        arguments.Add(targetPath);

        var result = await _ffmpegRunner.RunAsync(arguments, progress, cancellationToken);

        return result.Success
            ? ConversionResult.Succeeded(targetPath)
            : ConversionResult.Failed(result.ErrorMessage ?? "ffmpeg conversion failed.");
    }
}
