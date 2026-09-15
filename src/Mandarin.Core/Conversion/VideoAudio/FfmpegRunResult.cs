namespace Mandarin.Core.Conversion.VideoAudio;

public sealed class FfmpegRunResult
{
    private FfmpegRunResult(bool success, string? errorMessage)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    public string? ErrorMessage { get; }

    public static FfmpegRunResult Succeeded() => new(true, null);

    public static FfmpegRunResult Failed(string errorMessage) => new(false, errorMessage);
}
