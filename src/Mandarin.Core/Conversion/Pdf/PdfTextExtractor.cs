using UglyToad.PdfPig;

namespace Mandarin.Core.Conversion.Pdf;

/// <summary>
/// Extracts plain text from a PDF, one entry per page, using PdfPig.
/// </summary>
internal static class PdfTextExtractor
{
    public static IReadOnlyList<string> ExtractPageTexts(string pdfPath)
    {
        using var document = PdfDocument.Open(pdfPath);
        return document.GetPages().Select(page => page.Text).ToList();
    }
}
