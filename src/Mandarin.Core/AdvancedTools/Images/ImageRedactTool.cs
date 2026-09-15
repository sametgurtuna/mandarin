using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using ImageMagick.Drawing;
using Mandarin.Core.Conversion;

namespace Mandarin.Core.AdvancedTools.Images;

/// <summary>
/// Applies solid, blurred, or pixelated redactions to an image, matching Tangerine's Redact Photos.
/// </summary>
public sealed class ImageRedactTool : IAdvancedTool
{
    public AdvancedToolKind Kind => AdvancedToolKind.RedactPhoto;

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
        var redactOptions = options as RedactOptions ?? new RedactOptions(Array.Empty<RedactionRegion>());

        return Task.Run(() =>
        {
            try
            {
                progress?.Report(0);

                using var image = new MagickImage(sourcePath);
                cancellationToken.ThrowIfCancellationRequested();

                ApplyRedactions(image, redactOptions);

                cancellationToken.ThrowIfCancellationRequested();
                var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SuffixFor(AdvancedToolKind.RedactPhoto));
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
                return ConversionResult.Failed($"Could not redact '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }, cancellationToken);
    }

    public static void ApplyRedactions(MagickImage image, RedactOptions opt)
    {
        int w = (int)image.Width;
        int h = (int)image.Height;

        foreach (var r in opt.Regions)
        {
            int rx = Math.Max(0, (int)(r.NormalizedX * w));
            int ry = Math.Max(0, (int)(r.NormalizedY * h));
            int rw = Math.Min(w - rx, Math.Max(1, (int)(r.NormalizedWidth * w)));
            int rh = Math.Min(h - ry, Math.Max(1, (int)(r.NormalizedHeight * h)));

            if (rw <= 0 || rh <= 0) continue;

            if (r.Style == RedactStyle.Solid)
            {
                var color = MagickColor.FromRgba(0, 0, 0, 255);
                try
                {
                    color = new MagickColor(r.ColorHex);
                }
                catch { }

                new Drawables()
                    .FillColor(color)
                    .Rectangle(rx, ry, rx + rw, ry + rh)
                    .Draw(image);
            }
            else if (r.Style == RedactStyle.Blur)
            {
                using var sub = image.CloneArea(rx, ry, (uint)rw, (uint)rh);
                sub.GaussianBlur(15, 8);
                image.Composite(sub, rx, ry, CompositeOperator.Over);
            }
            else if (r.Style == RedactStyle.Pixelate)
            {
                using var sub = image.CloneArea(rx, ry, (uint)rw, (uint)rh);
                uint smallW = Math.Max(1, (uint)(rw / 12));
                uint smallH = Math.Max(1, (uint)(rh / 12));
                sub.FilterType = FilterType.Point;
                sub.Resize(smallW, smallH);
                sub.FilterType = FilterType.Point;
                sub.Resize((uint)rw, (uint)rh);
                image.Composite(sub, rx, ry, CompositeOperator.Over);
            }
        }
    }
}
