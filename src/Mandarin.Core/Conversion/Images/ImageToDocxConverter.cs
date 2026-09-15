using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ImageMagick;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace Mandarin.Core.Conversion.Images;

/// <summary>
/// Embeds a source image into a new single-page Word document, scaled to fit the page
/// while preserving aspect ratio.
/// </summary>
public sealed class ImageToDocxConverter : IConverter
{
    private const long PageWidthEmu = 6858000; // 7.5in usable width at 914400 EMU/in

    public IReadOnlySet<string> SourceExtensions { get; } = new HashSet<string>
    {
        "jpg", "jpeg", "png", "webp", "heic", "heif", "tiff", "tif", "bmp", "gif",
    };

    public IReadOnlySet<string> TargetExtensions { get; } = new HashSet<string> { "docx" };

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

                byte[] pngBytes;
                int pixelWidth, pixelHeight;
                using (var image = new MagickImage(sourcePath))
                {
                    pixelWidth = (int)image.Width;
                    pixelHeight = (int)image.Height;
                    image.Format = MagickFormat.Png;
                    pngBytes = image.ToByteArray();
                }

                cancellationToken.ThrowIfCancellationRequested();

                var (widthEmu, heightEmu) = ScaleToPage(pixelWidth, pixelHeight);

                using var document = WordprocessingDocument.Create(targetPath, WordprocessingDocumentType.Document);
                var mainPart = document.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());

                var imagePart = mainPart.AddImagePart(ImagePartType.Png);
                using (var stream = new MemoryStream(pngBytes))
                {
                    imagePart.FeedData(stream);
                }

                var imagePartId = mainPart.GetIdOfPart(imagePart);
                var body = mainPart.Document.Body!;
                body.AppendChild(BuildImageParagraph(imagePartId, widthEmu, heightEmu));
                mainPart.Document.Save();

                progress?.Report(1);
                return ConversionResult.Succeeded(targetPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException)
            {
                return ConversionResult.Failed($"Could not convert '{Path.GetFileName(sourcePath)}' to DOCX: {ex.Message}");
            }
        }, cancellationToken);
    }

    private static (long widthEmu, long heightEmu) ScaleToPage(int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            return (PageWidthEmu, PageWidthEmu);
        }

        var widthEmu = PageWidthEmu;
        var heightEmu = (long)(PageWidthEmu * ((double)pixelHeight / pixelWidth));
        return (widthEmu, heightEmu);
    }

    private static Paragraph BuildImageParagraph(string imagePartId, long widthEmu, long heightEmu)
    {
        var picture = new PIC.Picture(
            new PIC.NonVisualPictureProperties(
                new PIC.NonVisualDrawingProperties { Id = 0, Name = "Image" },
                new PIC.NonVisualPictureDrawingProperties()),
            new PIC.BlipFill(
                new A.Blip { Embed = imagePartId },
                new A.Stretch(new A.FillRectangle())),
            new PIC.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = 0, Y = 0 },
                    new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));

        var graphicData = new A.GraphicData(picture)
        {
            Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
        };

        var inline = new DW.Inline(
            new DW.Extent { Cx = widthEmu, Cy = heightEmu },
            new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
            new DW.DocProperties { Id = 1, Name = "Image" },
            new DW.NonVisualGraphicFrameDrawingProperties(
                new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(graphicData))
        {
            DistanceFromTop = 0,
            DistanceFromBottom = 0,
            DistanceFromLeft = 0,
            DistanceFromRight = 0,
        };

        return new Paragraph(new Run(new Drawing(inline)));
    }
}
