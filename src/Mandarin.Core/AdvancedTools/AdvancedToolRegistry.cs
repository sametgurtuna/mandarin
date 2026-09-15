namespace Mandarin.Core.AdvancedTools;

/// <summary>
/// Aggregates every registered <see cref="IAdvancedTool"/> and answers "which tools apply
/// to this file" / "which tool handles source + kind" queries.
/// </summary>
public sealed class AdvancedToolRegistry
{
    private readonly IReadOnlyList<IAdvancedTool> _tools;

    public AdvancedToolRegistry(IEnumerable<IAdvancedTool> tools)
    {
        _tools = tools.ToList();
    }

    public IReadOnlyList<AdvancedToolKind> GetAvailableTools(string sourceExtension)
    {
        return _tools
            .Where(t => t.SupportedExtensions.Contains(sourceExtension))
            .Select(t => t.Kind)
            .Distinct()
            .OrderBy(k => k)
            .ToList();
    }

    public IAdvancedTool? FindTool(string sourceExtension, AdvancedToolKind kind)
    {
        return _tools.FirstOrDefault(t => t.Kind == kind && t.SupportedExtensions.Contains(sourceExtension));
    }
}
