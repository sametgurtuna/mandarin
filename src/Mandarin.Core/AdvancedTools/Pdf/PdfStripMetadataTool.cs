using Mandarin.Core.Conversion;
using PdfSharpCore.Pdf.IO;

namespace Mandarin.Core.AdvancedTools.Pdf;

/// <summary>Clears the PDF's document-information dictionary (title, author, etc.).</summary>
public sealed class PdfStripMetadataTool : IAdvancedTool
{
    public AdvancedToolKind Kind => AdvancedToolKind.StripMetadata;

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string> { "pdf" };

    public Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                progress?.Report(0);

                using var document = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Modify);
                document.Info.Title = string.Empty;
                document.Info.Author = string.Empty;
                document.Info.Subject = string.Empty;
                document.Info.Keywords = string.Empty;
                document.Info.Creator = string.Empty;
                cancellationToken.ThrowIfCancellationRequested();

                var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SuffixFor(AdvancedToolKind.StripMetadata));
                document.Save(targetPath);

                progress?.Report(1);
                return ConversionResult.Succeeded(targetPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                return ConversionResult.Failed($"Could not strip metadata from '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }, cancellationToken);
    }
}
