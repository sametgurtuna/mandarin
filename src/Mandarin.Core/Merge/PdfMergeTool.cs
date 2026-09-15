using Mandarin.Core.Conversion;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace Mandarin.Core.Merge;

/// <summary>Concatenates several PDFs' pages, in the given order, into one new PDF.</summary>
public sealed class PdfMergeTool : IMergeTool
{
    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string> { "pdf" };

    public Task<ConversionResult> ExecuteAsync(
        IReadOnlyList<string> sourcePaths,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                progress?.Report(0);

                using var target = new PdfDocument();

                for (var i = 0; i < sourcePaths.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var source = PdfReader.Open(sourcePaths[i], PdfDocumentOpenMode.Import);
                    for (var pageIndex = 0; pageIndex < source.PageCount; pageIndex++)
                    {
                        target.AddPage(source.Pages[pageIndex], AnnotationCopyingType.DeepCopy);
                    }

                    progress?.Report((i + 1) / (double)sourcePaths.Count);
                }

                var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePaths[0], "(merged)");
                target.Save(targetPath);

                return ConversionResult.Succeeded(targetPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                return ConversionResult.Failed($"Could not merge PDFs: {ex.Message}");
            }
        }, cancellationToken);
    }
}
