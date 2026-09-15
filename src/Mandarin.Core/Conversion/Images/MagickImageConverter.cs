using ImageMagick;

namespace Mandarin.Core.Conversion.Images;

/// <summary>
/// Converts between raster/vector image formats using Magick.NET, including export to
/// PDF (each source image becomes a single-page PDF).
/// </summary>
public sealed class MagickImageConverter : IConverter
{
    public IReadOnlySet<string> SourceExtensions { get; } = new HashSet<string>
    {
        "jpg", "jpeg", "png", "webp", "heic", "heif", "tiff", "tif", "svg", "avif", "bmp", "gif",
    };

    public IReadOnlySet<string> TargetExtensions { get; } = new HashSet<string>
    {
        "jpg", "jpeg", "png", "webp", "heic", "tiff", "avif", "bmp", "gif", "pdf",
    };

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

                using var image = new MagickImage(sourcePath);
                cancellationToken.ThrowIfCancellationRequested();

                var format = MapExtensionToFormat(Path.GetExtension(targetPath).TrimStart('.').ToLowerInvariant());
                image.Format = format;

                image.Write(targetPath);
                progress?.Report(1);

                return ConversionResult.Succeeded(targetPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException)
            {
                return ConversionResult.Failed($"Could not convert '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }, cancellationToken);
    }

    private static MagickFormat MapExtensionToFormat(string extension) => extension switch
    {
        "jpg" or "jpeg" => MagickFormat.Jpg,
        "png" => MagickFormat.Png,
        "webp" => MagickFormat.WebP,
        "heic" => MagickFormat.Heic,
        "tiff" or "tif" => MagickFormat.Tiff,
        "avif" => MagickFormat.Avif,
        "bmp" => MagickFormat.Bmp,
        "gif" => MagickFormat.Gif,
        "pdf" => MagickFormat.Pdf,
        _ => throw new NotSupportedException($"Unsupported target image format '.{extension}'."),
    };
}
