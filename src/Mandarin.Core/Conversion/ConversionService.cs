using Mandarin.Core.Settings;

namespace Mandarin.Core.Conversion;

/// <summary>
/// Orchestrates a single-file conversion: resolves the converter, builds a non-clobbering
/// output path, runs the conversion, and remembers the chosen target format per source
/// file type for next time.
/// </summary>
public sealed class ConversionService
{
    private readonly ConverterRegistry _registry;
    private readonly ISettingsService _settingsService;
    private readonly MandarinSettings _settings;

    public ConversionService(ConverterRegistry registry, ISettingsService settingsService, MandarinSettings settings)
    {
        _registry = registry;
        _settingsService = settingsService;
        _settings = settings;
    }

    public bool IsSupported(string sourcePath) =>
        _registry.IsSourceSupported(FileExtensions.GetNormalizedExtension(sourcePath));

    public IReadOnlyList<string> GetAvailableTargets(string sourcePath) =>
        _registry.GetAvailableTargets(FileExtensions.GetNormalizedExtension(sourcePath));

    /// <summary>
    /// The most-likely conversion target for this file: the last format the user picked
    /// for this source extension, or the first available target if there's no history.
    /// </summary>
    public string? GetSuggestedTarget(string sourcePath)
    {
        var sourceExtension = FileExtensions.GetNormalizedExtension(sourcePath);
        var targets = _registry.GetAvailableTargets(sourceExtension);
        if (targets.Count == 0)
        {
            return null;
        }

        if (_settings.LastUsedFormatsByFileType.TryGetValue(sourceExtension, out var lastUsed) &&
            targets.Contains(lastUsed))
        {
            return lastUsed;
        }

        return targets[0];
    }

    public async Task<ConversionResult> ConvertAsync(
        string sourcePath,
        string targetExtension,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var sourceExtension = FileExtensions.GetNormalizedExtension(sourcePath);
        var converter = _registry.FindConverter(sourceExtension, targetExtension);
        if (converter is null)
        {
            return ConversionResult.Failed(
                $"No converter available for '.{sourceExtension}' -> '.{targetExtension}'.");
        }

        var outputPath = FileExtensions.BuildOutputPath(sourcePath, targetExtension);

        ConversionResult result;
        try
        {
            result = await converter.ConvertAsync(sourcePath, outputPath, progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Belt-and-suspenders: a converter must never take the app down, even on a
            // bug or an unanticipated exception type.
            return ConversionResult.Failed($"Could not convert '{Path.GetFileName(sourcePath)}': {ex.Message}");
        }

        if (result.Success)
        {
            _settings.LastUsedFormatsByFileType[sourceExtension] = targetExtension;
            _settingsService.Save(_settings);
        }

        return result;
    }
}
