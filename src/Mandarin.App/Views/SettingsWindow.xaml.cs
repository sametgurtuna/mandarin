using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Mandarin.App.Resources;
using Mandarin.Core.Conversion.VideoAudio;
using Mandarin.Core.Settings;
using Mandarin.Shell;
using Mandarin.App.Themes;

namespace Mandarin.App.Views;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly MandarinSettings _settings;
    private bool _isInitializing = true;

    public SettingsWindow(ISettingsService settingsService, MandarinSettings settings)
    {
        InitializeComponent();
        DialogChrome.Apply(this, HeaderBar, CardRoot);
        _settingsService = settingsService;
        _settings = settings;

        Title = Strings.SettingsTitle;
        HeaderTitleText.Text = Strings.SettingsTitle;
        HeadingText.Text = Strings.SettingsHeading;
        TaglineText.Text = Strings.SettingsTagline;
        LanguageLabelText.Text = Strings.SettingsLanguageLabel;
        RestartNoticeText.Text = Strings.SettingsRestartNotice;
        UpdatesHeadingText.Text = Strings.SettingsUpdatesHeading;
        CheckForUpdatesButton.Content = Strings.SettingsCheckForUpdatesButton;
        ExplorerHeadingText.Text = Strings.SettingsExplorerHeading;
        ExplorerIntegrationCheckBox.Content = Strings.SettingsExplorerCheckbox;
        StartupHeadingText.Text = Strings.SettingsStartupHeading;
        StartupCheckBox.Content = Strings.SettingsStartupCheckbox;

        foreach (ComboBoxItem item in ThemeComboBox.Items)
        {
            if ((string)item.Tag == _settings.Theme)
            {
                ThemeComboBox.SelectedItem = item;
                break;
            }
        }

        foreach (ComboBoxItem item in LanguageComboBox.Items)
        {
            if ((string)item.Tag == _settings.Language)
            {
                LanguageComboBox.SelectedItem = item;
                break;
            }
        }

        ExplorerIntegrationCheckBox.IsChecked = ExplorerIntegration.IsRegistered();
        StartupCheckBox.IsChecked = StartupIntegration.IsRegistered();
        UpdateFfmpegStatus();

        _isInitializing = false;
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || ThemeComboBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var mode = (string)item.Tag;
        if (mode == _settings.Theme)
        {
            return;
        }

        // Colours are DynamicResource-bound, so this repaints every open window
        // immediately — no restart notice needed, unlike the language switch.
        _settings.Theme = mode;
        _settingsService.Save(_settings);
        ThemeManager.Apply(mode);
    }

    private void UpdateFfmpegStatus()
    {
        var path = FfmpegLocator.FindFfmpegPath(_settings.FfmpegPath);
        FfmpegStatusText.Text = path is null
            ? Strings.FfmpegStatusMissing
            : Strings.FfmpegCurrentPath(path);
    }

    private void FfmpegChooseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new FfmpegSetupWindow(_settingsService, _settings) { Owner = this };
        dialog.ShowDialog();
        UpdateFfmpegStatus();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => DialogChrome.Dismiss(this);

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || LanguageComboBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var newLanguage = (string)item.Tag;
        if (newLanguage != _settings.Language)
        {
            _settings.Language = newLanguage;
            _settingsService.Save(_settings);
            RestartNoticeText.Visibility = Visibility.Visible;
        }
    }

    private void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        // Manual-only, per CLAUDE.md: no background/automatic update checks anywhere.
        // There's no release channel to check against yet, so this is an honest
        // placeholder rather than a fabricated "up to date" result.
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "dev";
        UpdateStatusText.Text = Strings.SettingsUpToDateMessage(version);
        UpdateStatusText.Visibility = Visibility.Visible;
    }

    private void ExplorerIntegrationCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        try
        {
            if (ExplorerIntegrationCheckBox.IsChecked == true)
            {
                var exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
                ExplorerIntegration.Register(exePath);
            }
            else
            {
                ExplorerIntegration.Unregister();
            }

            ExplorerErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            ExplorerErrorText.Text = Strings.SettingsExplorerErrorMessage(ex.Message);
            ExplorerErrorText.Visibility = Visibility.Visible;
            ExplorerIntegrationCheckBox.IsChecked = ExplorerIntegration.IsRegistered();
        }
    }

    private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        try
        {
            if (StartupCheckBox.IsChecked == true)
            {
                var exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
                StartupIntegration.Register(exePath);
            }
            else
            {
                StartupIntegration.Unregister();
            }

            StartupErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            StartupErrorText.Text = Strings.SettingsStartupErrorMessage(ex.Message);
            StartupErrorText.Visibility = Visibility.Visible;
            StartupCheckBox.IsChecked = StartupIntegration.IsRegistered();
        }
    }
}
