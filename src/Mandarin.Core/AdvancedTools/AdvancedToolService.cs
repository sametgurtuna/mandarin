using Mandarin.Core.Conversion;

namespace Mandarin.Core.AdvancedTools;

/// <summary>
/// Orchestrates running one advanced tool against one file: resolves the tool and
/// guarantees a <see cref="ConversionResult"/> comes back even if the tool throws.
/// </summary>
public sealed class AdvancedToolService
{
    private readonly AdvancedToolRegistry _registry;

    public AdvancedToolService(AdvancedToolRegistry registry)
    {
        _registry = registry;
    }

    public IReadOnlyList<AdvancedToolKind> GetAvailableTools(string sourcePath) =>
        _registry.GetAvailableTools(FileExtensions.GetNormalizedExtension(sourcePath));

    public async Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        AdvancedToolKind kind,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var sourceExtension = FileExtensions.GetNormalizedExtension(sourcePath);
        var tool = _registry.FindTool(sourceExtension, kind);
        if (tool is null)
        {
            return ConversionResult.Failed($"No '{kind}' tool available for '.{sourceExtension}' files.");
        }

        try
        {
            return await tool.ExecuteAsync(sourcePath, options, progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ConversionResult.Failed($"Could not {kind.ToString().ToLowerInvariant()} '{Path.GetFileName(sourcePath)}': {ex.Message}");
        }
    }
}
