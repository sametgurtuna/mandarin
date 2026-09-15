namespace Mandarin.Core.Settings;

/// <summary>
/// User settings persisted as JSON under %AppData%\Mandarin\settings.json.
/// </summary>
public sealed class MandarinSettings
{
    public double PanelX { get; set; } = 100;

    public double PanelY { get; set; } = 100;

    public string Language { get; set; } = "en";

    /// <summary>"system", "light" or "dark". "system" follows the Windows app mode.</summary>
    public string Theme { get; set; } = "system";

    /// <summary>
    /// Where the user pointed us at ffmpeg.exe, when it wasn't found automatically.
    /// Null means "look in the usual places" (see FfmpegLocator).
    /// </summary>
    public string? FfmpegPath { get; set; }

    public Dictionary<string, string> LastUsedFormatsByFileType { get; set; } = new();
}
