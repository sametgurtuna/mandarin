using ImageMagick;
using Mandarin.Core.Conversion;

namespace Mandarin.Core.AdvancedTools.Images;

/// <summary>Re-encodes an image at a lower quality — and optionally smaller — to shrink its file size.</summary>
public sealed class ImageCompressTool : IAdvancedTool
{
    /// <summary>How many quality probes a target-size search is allowed.</summary>
    private const int TargetSearchSteps = 7;

    /// <summary>Below this, quality artefacts are worse than a slightly bigger file.</summary>
    private const int MinSearchQuality = 20;

    /// <summary>Downscale factors tried, in order, when quality alone can't reach the target.</summary>
    private static readonly double[] FallbackScales = { 0.75, 0.5, 0.35 };

    public AdvancedToolKind Kind => AdvancedToolKind.Compress;

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
        var settings = options as CompressOptions ?? new CompressOptions(75);
        var quality = Math.Clamp(settings.Quality, 1, 100);

        return Task.Run(() =>
        {
            try
            {
                progress?.Report(0);

                var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SuffixFor(AdvancedToolKind.Compress));

                if (settings.TargetSizeBytes is { } targetBytes && targetBytes > 0)
                {
                    WriteWithinTargetSize(sourcePath, targetPath, settings, targetBytes, progress, cancellationToken);
                }
                else
                {
                    using var image = LoadAndPrepare(sourcePath, settings, scale: 1.0);
                    ApplyQuality(image, settings, quality);
                    cancellationToken.ThrowIfCancellationRequested();
                    image.Write(targetPath);
                }

                progress?.Report(1);
                return ConversionResult.Succeeded(targetPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException)
            {
                return ConversionResult.Failed($"Could not compress '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }, cancellationToken);
    }

    private static MagickImage LoadAndPrepare(string sourcePath, CompressOptions settings, double scale)
    {
        var image = new MagickImage(sourcePath);

        // Cap the long edge if asked, then apply any extra downscale the
        // target-size search is trying. Only ever shrink.
        var longEdge = Math.Max(image.Width, image.Height);
        var limit = settings.MaxLongEdge is { } max && max > 0 ? Math.Min(max, (int)longEdge) : (int)longEdge;
        var effective = (int)Math.Round(limit * scale);

        if (effective > 0 && effective < longEdge)
        {
            var factor = effective / (double)longEdge;
            image.Resize((uint)Math.Max(1, Math.Round(image.Width * factor)),
                         (uint)Math.Max(1, Math.Round(image.Height * factor)));
        }

        return image;
    }

    private static void ApplyQuality(MagickImage image, CompressOptions settings, int quality)
    {
        if (settings.Lossless)
        {
            // WebP and PNG read this; for formats without a lossless mode it is
            // simply the top quality setting.
            image.Settings.SetDefine(MagickFormat.WebP, "lossless", "true");
            image.Quality = 100;
            return;
        }

        image.Quality = (uint)Math.Clamp(quality, 1, 100);
    }

    /// <summary>
    /// Searches for the highest quality that still fits the target size, then
    /// falls back to downscaling if even low quality is too big. Each probe is
    /// written to a temp file so the output is only produced once, at the end.
    /// </summary>
    private static void WriteWithinTargetSize(
        string sourcePath,
        string targetPath,
        CompressOptions settings,
        long targetBytes,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var extension = FileExtensions.GetNormalizedExtension(targetPath);
        var probePath = Path.Combine(Path.GetTempPath(), $"Mandarin_compress_{Guid.NewGuid()}.{extension}");

        try
        {
            byte[]? best = null;

            foreach (var scale in new[] { 1.0 }.Concat(FallbackScales))
            {
                var low = MinSearchQuality;
                var high = Math.Clamp(settings.Quality, MinSearchQuality, 100);

                for (var step = 0; step < TargetSearchSteps && low <= high; step++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var probeQuality = (low + high) / 2;
                    using (var image = LoadAndPrepare(sourcePath, settings, scale))
                    {
                        ApplyQuality(image, settings, probeQuality);
                        image.Write(probePath);
                    }

                    var size = new FileInfo(probePath).Length;
                    progress?.Report(Math.Min(0.95, (step + 1) / (double)TargetSearchSteps));

                    if (size <= targetBytes)
                    {
                        // Fits: keep it and try for better quality.
                        best = File.ReadAllBytes(probePath);
                        low = probeQuality + 1;
                    }
                    else
                    {
                        high = probeQuality - 1;
                    }
                }

                if (best is not null)
                {
                    break;
                }
            }

            if (best is null)
            {
                // Nothing fit; write the smallest thing we can rather than failing,
                // so the user still gets a result they can judge.
                using var image = LoadAndPrepare(sourcePath, settings, FallbackScales[^1]);
                ApplyQuality(image, settings, MinSearchQuality);
                image.Write(targetPath);
                return;
            }

            File.WriteAllBytes(targetPath, best);
        }
        finally
        {
            try
            {
                if (File.Exists(probePath))
                {
                    File.Delete(probePath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A leftover temp probe is harmless.
            }
        }
    }
}
