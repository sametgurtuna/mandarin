using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using Mandarin.Core.Conversion;

namespace Mandarin.Core.AdvancedTools.Images;

/// <summary>
/// Adjusts image light (exposure, brightness, contrast, gamma), color (temperature, tint, saturation, hue),
/// and detail (clarity, noise reduction), matching Tangerine's Edit Photos tool.
/// </summary>
public sealed class ImageEditTool : IAdvancedTool
{
    public AdvancedToolKind Kind => AdvancedToolKind.EditPhoto;

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>
    {
        "jpg", "jpeg", "png", "webp", "tiff", "tif", "avif", "bmp", "heic"
    };

    public Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var editOptions = options as ImageEditOptions ?? new ImageEditOptions();

        return Task.Run(() =>
        {
            try
            {
                progress?.Report(0);

                using var image = new MagickImage(sourcePath);
                cancellationToken.ThrowIfCancellationRequested();

                // Apply Light & Color adjustments
                ApplyAdjustments(image, editOptions);

                cancellationToken.ThrowIfCancellationRequested();
                var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SuffixFor(AdvancedToolKind.EditPhoto));
                image.Write(targetPath);

                progress?.Report(1);
                return ConversionResult.Succeeded(targetPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ConversionResult.Failed($"Could not edit '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }, cancellationToken);
    }

    public static void ApplyAdjustments(MagickImage image, ImageEditOptions opt)
    {
        // Brightness & Contrast & Exposure
        double effectiveBrightness = opt.Brightness + (opt.Exposure * 15.0);
        if (Math.Abs(effectiveBrightness) > 0.1 || Math.Abs(opt.Contrast) > 0.1)
        {
            image.BrightnessContrast(new Percentage(effectiveBrightness), new Percentage(opt.Contrast));
        }

        // Gamma
        if (Math.Abs(opt.Gamma - 1.0) > 0.01)
        {
            image.GammaCorrect(Math.Max(0.1, opt.Gamma));
        }

        // Saturation & Hue
        if (Math.Abs(opt.Saturation) > 0.1 || Math.Abs(opt.Hue) > 0.1)
        {
            double sat = Math.Max(0, 100 + opt.Saturation);
            double hue = 100 + opt.Hue;
            image.Modulate(new Percentage(100), new Percentage(sat), new Percentage(hue));
        }

        // Clarity (sharpen)
        if (opt.Clarity > 1.0)
        {
            image.Sharpen(0, opt.Clarity * 0.05);
        }

        // Noise Reduction (Despeckle / ReduceNoise)
        if (opt.NoiseReduction > 5.0)
        {
            image.Despeckle();
        }
    }
}
