namespace Mandarin.Core.Conversion;

/// <summary>
/// Aggregates every registered <see cref="IConverter"/> and answers "what can this file
/// become" / "which converter handles source -> target" queries. New formats plug in by
/// registering another <see cref="IConverter"/>, not by editing this class.
/// </summary>
public sealed class ConverterRegistry
{
    private readonly IReadOnlyList<IConverter> _converters;

    public ConverterRegistry(IEnumerable<IConverter> converters)
    {
        _converters = converters.ToList();
    }

    public bool IsSourceSupported(string sourceExtension) =>
        _converters.Any(c => c.TargetExtensions.Any(target => c.CanConvert(sourceExtension, target)));

    /// <summary>
    /// Every target extension reachable from the given source extension, across all
    /// registered converters, sorted alphabetically. Defers to each converter's own
    /// <see cref="IConverter.CanConvert"/> (not just its declared extension sets), so a
    /// converter that restricts specific source/target pairs — or that accepts a wildcard
    /// source — is reflected correctly here.
    /// </summary>
    public IReadOnlyList<string> GetAvailableTargets(string sourceExtension)
    {
        return _converters
            .SelectMany(c => c.TargetExtensions.Where(target => c.CanConvert(sourceExtension, target)))
            .Where(target => target != sourceExtension)
            .Distinct()
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IConverter? FindConverter(string sourceExtension, string targetExtension)
    {
        return _converters.FirstOrDefault(c => c.CanConvert(sourceExtension, targetExtension));
    }
}
