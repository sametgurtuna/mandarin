using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SkiaSharp;
using OpenXmlText = DocumentFormat.OpenXml.Wordprocessing.Text;
using PdfToImageConversion = PDFtoImage.Conversion;
using PdfToImageRenderOptions = PDFtoImage.RenderOptions;

namespace Mandarin.Core.Conversion.Pdf;

/// <summary>
/// Converts a PDF to DOCX (plain extracted text), TXT, or JPG/PNG (each page rasterized
/// at 300 DPI via PDFium/PDFtoImage).
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class PdfConverter : IConverter
{
    private const int RasterDpi = 300;

    public IReadOnlySet<string> SourceExtensions { get; } = new HashSet<string> { "pdf" };

    public IReadOnlySet<string> TargetExtensions { get; } = new HashSet<string> { "docx", "jpg", "png", "txt" };

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

                var targetExtension = FileExtensions.GetNormalizedExtension(targetPath);
                var result = targetExtension switch
                {
                    "txt" => ConvertToTxt(sourcePath, targetPath),
                    "docx" => ConvertToDocx(sourcePath, targetPath),
                    "jpg" or "png" => ConvertToImages(sourcePath, targetPath, targetExtension, progress, cancellationToken),
                    _ => ConversionResult.Failed($"Unsupported PDF target format '.{targetExtension}'."),
                };

                progress?.Report(1);
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ConversionResult.Failed($"Could not convert '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }, cancellationToken);
    }

    private static ConversionResult ConvertToTxt(string sourcePath, string targetPath)
    {
        var pageTexts = PdfTextExtractor.ExtractPageTexts(sourcePath);
        var content = string.Join(Environment.NewLine + Environment.NewLine, pageTexts);
        File.WriteAllText(targetPath, content, System.Text.Encoding.UTF8);
        return ConversionResult.Succeeded(targetPath);
    }

    private static ConversionResult ConvertToDocx(string sourcePath, string targetPath)
    {
        var pageTexts = PdfTextExtractor.ExtractPageTexts(sourcePath);

        using var document = WordprocessingDocument.Create(targetPath, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        var body = mainPart.Document.Body!;

        for (var i = 0; i < pageTexts.Count; i++)
        {
            foreach (var line in pageTexts[i].Split('\n'))
            {
                body.AppendChild(new Paragraph(new Run(new OpenXmlText(line))));
            }

            if (i < pageTexts.Count - 1)
            {
                body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
            }
        }

        mainPart.Document.Save();
        return ConversionResult.Succeeded(targetPath);
    }

    private static ConversionResult ConvertToImages(
        string sourcePath,
        string targetPath,
        string targetExtension,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(sourcePath);
        var bitmaps = PdfToImageConversion.ToImages(stream, leaveOpen: false, password: null, options: new PdfToImageRenderOptions(Dpi: RasterDpi)).ToList();

        if (bitmaps.Count == 0)
        {
            return ConversionResult.Failed("The PDF has no pages to render.");
        }

        var directory = Path.GetDirectoryName(targetPath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(targetPath);
        var format = targetExtension == "png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;

        string? firstOutputPath = null;
        for (var i = 0; i < bitmaps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outputPath = bitmaps.Count == 1
                ? targetPath
                : FileExtensions.BuildOutputPath(Path.Combine(directory, $"{baseName} (page {i + 1})"), targetExtension);

            using (var bitmap = bitmaps[i])
            {
                using var data = bitmap.Encode(format, 90);
                if (data != null)
                {
                    using var fileStream = File.Create(outputPath);
                    data.SaveTo(fileStream);
                }
                else
                {
                    using var image = SKImage.FromBitmap(bitmap);
                    using var imgData = image.Encode(format, 90);
                    if (imgData != null)
                    {
                        using var fileStream = File.Create(outputPath);
                        imgData.SaveTo(fileStream);
                    }
                }
            }

            firstOutputPath ??= outputPath;
            progress?.Report((i + 1) / (double)bitmaps.Count);
        }

        return ConversionResult.Succeeded(firstOutputPath!);
    }
}
