using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SkiaSharp;

namespace Mandarin.Core.Conversion.Text;

/// <summary>
/// Exports a plain-text file to PDF (paginated) or to a single tall JPG/PNG screenshot of
/// the wrapped text.
/// </summary>
public sealed class TextExportConverter : IConverter
{
    private const double PageMarginPt = 40;
    private const double FontSizePt = 11;

    private const int ImageWidthPx = 1240;
    private const int ImageMarginPx = 48;
    private const float ImageFontSizePx = 24;

    public IReadOnlySet<string> SourceExtensions { get; } = new HashSet<string> { "txt" };

    public IReadOnlySet<string> TargetExtensions { get; } = new HashSet<string> { "pdf", "jpg", "png" };

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

                var text = File.ReadAllText(sourcePath);
                var targetExtension = FileExtensions.GetNormalizedExtension(targetPath);

                var result = targetExtension == "pdf"
                    ? ConvertToPdf(text, targetPath)
                    : ConvertToImage(text, targetPath, targetExtension);

                progress?.Report(1);
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return ConversionResult.Failed($"Could not convert '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }, cancellationToken);
    }

    private static ConversionResult ConvertToPdf(string text, string targetPath)
    {
        using var document = new PdfDocument();
        var font = new XFont("Verdana", FontSizePt, XFontStyle.Regular);

        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        var lineHeight = font.GetHeight() * 1.25;
        var y = PageMarginPt;
        var maxWidth = page.Width - (2 * PageMarginPt);
        var maxY = page.Height - PageMarginPt;

        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            foreach (var line in WrapLine(gfx, font, paragraph, maxWidth))
            {
                if (y + lineHeight > maxY)
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = PageMarginPt;
                }

                gfx.DrawString(line, font, XBrushes.Black, new XPoint(PageMarginPt, y + font.GetHeight()));
                y += lineHeight;
            }
        }

        document.Save(targetPath);
        return ConversionResult.Succeeded(targetPath);
    }

    private static IEnumerable<string> WrapLine(XGraphics gfx, XFont font, string paragraph, double maxWidth)
    {
        if (paragraph.Length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        var words = paragraph.Split(' ');
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (gfx.MeasureString(candidate, font).Width > maxWidth && current.Length > 0)
            {
                yield return current;
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        yield return current;
    }

    private static ConversionResult ConvertToImage(string text, string targetPath, string targetExtension)
    {
        using var font = new SKFont(SKTypeface.Default, ImageFontSizePx);
        var maxWidth = ImageWidthPx - (2 * ImageMarginPx);
        var lines = new List<string>();

        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            lines.AddRange(WrapLine(font, paragraph, maxWidth));
        }

        font.GetFontMetrics(out var metrics);
        var lineHeight = metrics.Descent - metrics.Ascent + metrics.Leading;
        var height = (int)Math.Ceiling((2 * ImageMarginPx) + (lines.Count * lineHeight));
        height = Math.Max(height, ImageMarginPx * 2 + (int)lineHeight);

        using var bitmap = new SKBitmap(ImageWidthPx, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var textPaint = new SKPaint { IsAntialias = true, Color = SKColors.Black };

        var y = ImageMarginPx - metrics.Ascent;
        foreach (var line in lines)
        {
            canvas.DrawText(line, ImageMarginPx, y, SKTextAlign.Left, font, textPaint);
            y += lineHeight;
        }

        var format = targetExtension == "png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
        using var fileStream = File.Create(targetPath);
        using var data = bitmap.Encode(format, 90);
        data.SaveTo(fileStream);

        return ConversionResult.Succeeded(targetPath);
    }

    private static IEnumerable<string> WrapLine(SKFont font, string paragraph, double maxWidth)
    {
        if (paragraph.Length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        var words = paragraph.Split(' ');
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (font.MeasureText(candidate) > maxWidth && current.Length > 0)
            {
                yield return current;
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        yield return current;
    }
}
