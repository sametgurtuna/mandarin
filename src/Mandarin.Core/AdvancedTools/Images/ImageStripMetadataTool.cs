using ImageMagick;
using Mandarin.Core.Conversion;

namespace Mandarin.Core.AdvancedTools.Images;

/// <summary>Removes EXIF and other embedded profiles/metadata from an image.</summary>
public sealed class ImageStripMetadataTool : IAdvancedTool
{
    public AdvancedToolKind Kind => AdvancedToolKind.StripMetadata;

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>
    {
        "jpg", "jpeg", "png", "webp", "tiff", "tif", "avif", "bmp", "heic",
    };

    public Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                progress?.Report(0);

                using var image = new MagickImage(sourcePath);
                image.Strip();
                cancellationToken.ThrowIfCancellationRequested();

                var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SuffixFor(AdvancedToolKind.StripMetadata));
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
                return ConversionResult.Failed($"Could not strip metadata from '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }, cancellationToken);
    }
}
