using ImageMagick;
using Mandarin.Core.Conversion;

namespace Mandarin.Core.AdvancedTools.Images;

/// <summary>Crops an image to a pixel rectangle chosen by the user.</summary>
public sealed class ImageCropTool : IAdvancedTool
{
    public AdvancedToolKind Kind => AdvancedToolKind.Crop;

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>
    {
        "jpg", "jpeg", "png", "webp", "tiff", "tif", "avif", "bmp", "gif", "heic",
    };

    public Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (options is not CropOptions crop || crop.Width <= 0 || crop.Height <= 0)
        {
            return Task.FromResult(ConversionResult.Failed("No crop rectangle was selected."));
        }

        return Task.Run(() =>
        {
            try
            {
                progress?.Report(0);

                using var image = new MagickImage(sourcePath);
                var geometry = new MagickGeometry(crop.X, crop.Y, (uint)crop.Width, (uint)crop.Height);
                image.Crop(geometry);
                image.ResetPage();
                cancellationToken.ThrowIfCancellationRequested();

                var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SuffixFor(AdvancedToolKind.Crop));
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
                return ConversionResult.Failed($"Could not crop '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }, cancellationToken);
    }
}
