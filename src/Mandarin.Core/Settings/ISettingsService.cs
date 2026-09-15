namespace Mandarin.Core.Settings;

public interface ISettingsService
{
    MandarinSettings Load();

    void Save(MandarinSettings settings);
}
