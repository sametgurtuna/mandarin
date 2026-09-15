using Mandarin.Core.Conversion;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SkiaSharp;
using PdfToImageConversion = PDFtoImage.Conversion;
using PdfToImageRenderOptions = PDFtoImage.RenderOptions;

namespace Mandarin.Core.AdvancedTools.Pdf;

/// <summary>
/// Shrinks a PDF by rasterizing every page at a lower DPI/JPEG quality and rebuilding an
/// image-based PDF from the results. Lossy, but effective when the original's size comes
/// from high-resolution embedded images (the common case for scanned documents).
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class PdfCompressTool : IAdvancedTool
{
    public AdvancedToolKind Kind => AdvancedToolKind.Compress;

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string> { "pdf" };

    public Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var quality = Math.Clamp((options as CompressOptions)?.Quality ?? 50, 1, 100);
        var dpi = 72 + (int)(quality / 100.0 * 128); // 72-200 DPI
        var jpegQuality = 30 + (int)(quality / 100.0 * 60); // 30-90

        return Task.Run(() =>
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "Mandarin_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDirectory);

            try
            {
                progress?.Report(0);

                using var stream = File.OpenRead(sourcePath);
                var bitmaps = PdfToImageConversion.ToImages(stream, leaveOpen: false, password: null, options: new PdfToImageRenderOptions(Dpi: dpi)).ToList();

                if (bitmaps.Count == 0)
                {
                    return ConversionResult.Failed("The PDF has no pages to compress.");
                }

                using var target = new PdfDocument();

                for (var i = 0; i < bitmaps.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var bitmap = bitmaps[i];
                    var tempImagePath = Path.Combine(tempDirectory, $"page{i}.jpg");
                    using (var fileStream = File.Create(tempImagePath))
                    using (var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, jpegQuality))
                    {
                        data.SaveTo(fileStream);
                    }

                    var xImage = XImage.FromFile(tempImagePath);
                    var page = target.AddPage();
                    page.Width = XUnit.FromPoint(bitmap.Width * 72.0 / dpi);
                    page.Height = XUnit.FromPoint(bitmap.Height * 72.0 / dpi);

                    using var gfx = XGraphics.FromPdfPage(page);
                    gfx.DrawImage(xImage, 0, 0, page.Width, page.Height);

                    progress?.Report((i + 1) / (double)bitmaps.Count);
                }

                var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SuffixFor(AdvancedToolKind.Compress));
                target.Save(targetPath);

                return ConversionResult.Succeeded(targetPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                return ConversionResult.Failed($"Could not compress '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
                catch (IOException)
                {
                    // Best-effort cleanup only.
                }
            }
        }, cancellationToken);
    }
}
