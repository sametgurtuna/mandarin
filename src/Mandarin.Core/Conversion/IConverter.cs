namespace Mandarin.Core.Conversion;

/// <summary>
/// Strategy interface for a single conversion capability (e.g. "image formats via
/// Magick.NET", "image to DOCX"). Add new formats by implementing this interface, never
/// by growing an if/else or switch chain over extensions.
/// </summary>
public interface IConverter
{
    /// <summary>
    /// File extensions this converter can read from, without the leading dot, lowercase.
    /// </summary>
    IReadOnlySet<string> SourceExtensions { get; }

    /// <summary>
    /// File extensions this converter can write to, without the leading dot, lowercase.
    /// </summary>
    IReadOnlySet<string> TargetExtensions { get; }

    bool CanConvert(string sourceExtension, string targetExtension) =>
        SourceExtensions.Contains(sourceExtension) && TargetExtensions.Contains(targetExtension);

    Task<ConversionResult> ConvertAsync(
        string sourcePath,
        string targetPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}
