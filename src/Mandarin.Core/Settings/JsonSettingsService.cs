using System.Text.Json;

namespace Mandarin.Core.Settings;

/// <summary>
/// Persists <see cref="MandarinSettings"/> as JSON on disk. Never throws on a missing or
/// corrupt settings file — falls back to defaults instead.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsFilePath;

    public JsonSettingsService(string? settingsDirectory = null)
    {
        var directory = settingsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mandarin");

        _settingsFilePath = Path.Combine(directory, "settings.json");
    }

    public MandarinSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new MandarinSettings();
            }

            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<MandarinSettings>(json, JsonOptions) ?? new MandarinSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new MandarinSettings();
        }
    }

    public void Save(MandarinSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings persistence is best-effort; never crash the app over a save failure.
        }
    }
}
