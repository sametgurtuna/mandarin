using PdfSharpCore.Pdf.IO;

namespace Mandarin.Core.Conversion.Pdf;

public static class PdfPageCounter
{
    public static int? TryGetPageCount(string pdfPath)
    {
        try
        {
            using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.InformationOnly);
            return document.PageCount;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return null;
        }
    }
}
