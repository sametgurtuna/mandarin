using Mandarin.Core.Conversion;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace Mandarin.Core.AdvancedTools.Pdf;

/// <summary>Extracts a page range into a new PDF (e.g. pages 3-7 of a larger document).</summary>
public sealed class PdfSplitTool : IAdvancedTool
{
    public AdvancedToolKind Kind => AdvancedToolKind.Split;

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string> { "pdf" };

    public Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (options is not PdfSplitOptions split || split.FromPage < 1 || split.ToPage < split.FromPage)
        {
            return Task.FromResult(ConversionResult.Failed("Invalid page range."));
        }

        return Task.Run(() =>
        {
            try
            {
                progress?.Report(0);

                using var source = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
                var fromPage = Math.Max(1, split.FromPage);
                var toPage = Math.Min(source.PageCount, split.ToPage);

                if (fromPage > source.PageCount)
                {
                    return ConversionResult.Failed(
                        $"'{Path.GetFileName(sourcePath)}' only has {source.PageCount} page(s).");
                }

                using var target = new PdfDocument();
                for (var i = fromPage; i <= toPage; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    target.AddPage(source.Pages[i - 1], AnnotationCopyingType.DeepCopy);
                    progress?.Report((i - fromPage + 1) / (double)(toPage - fromPage + 1));
                }

                var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.PageRangeSuffix(fromPage, toPage));
                target.Save(targetPath);

                return ConversionResult.Succeeded(targetPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                return ConversionResult.Failed($"Could not split '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }, cancellationToken);
    }
}
