using System.Text;

namespace Mandarin.Core.Conversion.Text;

/// <summary>
/// Converts between SRT, WebVTT, and plain TXT using a small hand-rolled parser (no
/// heavy subtitle library needed). Converting TXT -> SRT/VTT treats each line as a cue
/// with an evenly-spaced generated timestamp; converting a subtitle format -> TXT strips
/// timing and just keeps the dialogue.
/// </summary>
public sealed class SubtitleConverter : IConverter
{
    public IReadOnlySet<string> SourceExtensions { get; } = new HashSet<string> { "txt", "srt", "vtt" };

    public IReadOnlySet<string> TargetExtensions { get; } = new HashSet<string> { "txt", "srt", "vtt" };

    public Task<ConversionResult> ConvertAsync(
        string sourcePath,
        string targetPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                progress?.Report(0);

                var sourceExtension = FileExtensions.GetNormalizedExtension(sourcePath);
                var targetExtension = FileExtensions.GetNormalizedExtension(targetPath);
                var content = File.ReadAllText(sourcePath, Encoding.UTF8);

                var cues = sourceExtension switch
                {
                    "srt" => SubtitleFormat.ParseSrt(content),
                    "vtt" => SubtitleFormat.ParseVtt(content),
                    "txt" => SubtitleFormat.FromPlainText(content),
                    _ => throw new NotSupportedException($"Unsupported subtitle source '.{sourceExtension}'."),
                };

                var output = targetExtension switch
                {
                    "srt" => SubtitleFormat.ToSrt(cues),
                    "vtt" => SubtitleFormat.ToVtt(cues),
                    "txt" => SubtitleFormat.ToPlainText(cues),
                    _ => throw new NotSupportedException($"Unsupported subtitle target '.{targetExtension}'."),
                };

                File.WriteAllText(targetPath, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                progress?.Report(1);
                return ConversionResult.Succeeded(targetPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FormatException)
            {
                return ConversionResult.Failed($"Could not convert '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }, cancellationToken);
    }
}
