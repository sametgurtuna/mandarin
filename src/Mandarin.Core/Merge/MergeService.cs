using Mandarin.Core.Conversion;

namespace Mandarin.Core.Merge;

/// <summary>
/// Orchestrates merging: requires at least 2 files that all share one extension supported
/// by some registered <see cref="IMergeTool"/> (merging different file types isn't
/// supported — the tools themselves don't know how to reconcile e.g. a PDF with an MP4).
/// </summary>
public sealed class MergeService
{
    private readonly IReadOnlyList<IMergeTool> _tools;

    public MergeService(IEnumerable<IMergeTool> tools)
    {
        _tools = tools.ToList();
    }

    public bool CanMerge(IReadOnlyList<string> sourcePaths)
    {
        if (sourcePaths.Count < 2)
        {
            return false;
        }

        var extensions = sourcePaths.Select(FileExtensions.GetNormalizedExtension).Distinct().ToList();
        return extensions.Count == 1 && _tools.Any(t => t.SupportedExtensions.Contains(extensions[0]));
    }

    public async Task<ConversionResult> MergeAsync(
        IReadOnlyList<string> sourcePaths,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (!CanMerge(sourcePaths))
        {
            return ConversionResult.Failed("These files can't be merged (need 2+ files of the same supported type).");
        }

        var extension = FileExtensions.GetNormalizedExtension(sourcePaths[0]);
        var tool = _tools.First(t => t.SupportedExtensions.Contains(extension));

        try
        {
            return await tool.ExecuteAsync(sourcePaths, progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ConversionResult.Failed($"Could not merge these files: {ex.Message}");
        }
    }
}
