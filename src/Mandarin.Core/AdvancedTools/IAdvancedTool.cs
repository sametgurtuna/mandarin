using Mandarin.Core.Conversion;

namespace Mandarin.Core.AdvancedTools;

/// <summary>
/// Strategy interface for one advanced (Shift+Alt) tool applied to one file type. Add a
/// new file type's support for a tool by implementing this interface, never by growing an
/// if/else chain. Each tool owns its own output filename convention (e.g.
/// "photo (cropped).jpg") since that varies per tool.
/// </summary>
public interface IAdvancedTool
{
    AdvancedToolKind Kind { get; }

    IReadOnlySet<string> SupportedExtensions { get; }

    Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}
