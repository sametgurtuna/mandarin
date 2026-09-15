namespace Mandarin.Core.Conversion;

public sealed class ConversionResult
{
    private ConversionResult(bool success, string? outputPath, string? errorMessage, string? warning = null)
    {
        Success = success;
        OutputPath = outputPath;
        ErrorMessage = errorMessage;
        Warning = warning;
    }

    public bool Success { get; }

    public string? OutputPath { get; }

    public string? ErrorMessage { get; }

    /// <summary>
    /// Set when the work finished but didn't fully meet what was asked — for
    /// example a target size the encoder couldn't reach without destroying the
    /// file. The output is still valid; the user just deserves to be told.
    /// </summary>
    public string? Warning { get; }

    public static ConversionResult Succeeded(string outputPath) => new(true, outputPath, null);

    public static ConversionResult SucceededWithWarning(string outputPath, string warning) =>
        new(true, outputPath, null, warning);

    public static ConversionResult Failed(string errorMessage) => new(false, null, errorMessage);
}
