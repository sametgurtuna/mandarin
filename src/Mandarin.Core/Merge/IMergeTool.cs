using Mandarin.Core.Conversion;

namespace Mandarin.Core.Merge;

/// <summary>
/// Strategy interface for combining several files of the same type into one. Unlike
/// <see cref="IConverter"/> and the advanced tools, this operates on a list of sources.
/// </summary>
public interface IMergeTool
{
    IReadOnlySet<string> SupportedExtensions { get; }

    Task<ConversionResult> ExecuteAsync(
        IReadOnlyList<string> sourcePaths,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}
