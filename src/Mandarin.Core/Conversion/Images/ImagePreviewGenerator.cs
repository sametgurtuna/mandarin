using ImageMagick;

namespace Mandarin.Core.Conversion.Images;

/// <summary>
/// Renders any supported source image to a temporary PNG, so a WPF preview (which can't
/// natively decode every format Magick.NET reads, e.g. AVIF/HEIC) always has something it
/// can display.
/// </summary>
public static class ImagePreviewGenerator
{
    public static string GeneratePreviewPng(string sourcePath)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"Mandarin_preview_{Guid.NewGuid()}.png");

        using var image = new MagickImage(sourcePath);
        image.Format = MagickFormat.Png;
        image.Write(tempPath);

        return tempPath;
    }
}
